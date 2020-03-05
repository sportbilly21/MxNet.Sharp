﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxNet.Initializers;
using MxNet.KVstore;
using MxNet.Optimizers;

namespace MxNet.Gluon
{
    public class Trainer
    {
        private readonly Dictionary<string, object> _compression_params;
        private readonly bool _contains_sparse_grad;
        private readonly bool _contains_sparse_weight;
        private readonly Context[] _contexts;
        private bool? _distributed;
        private Initializer _init_optimizer;
        internal bool _kv_initialized;
        internal KVStore _kvstore;
        private readonly Dictionary<string, object> _kvstore_params = new Dictionary<string, object>();
        private readonly Dictionary<string, int> _param2idx = new Dictionary<string, int>();
        private readonly List<Parameter> _params;
        internal List<Parameter> _params_to_init;
        private readonly float _scale;
        internal bool? _update_on_kvstore;
        private List<Updater> _updaters;

        public Trainer(ParameterDict @params, Optimizer optimizer, string kvstore = "device",
            Dictionary<string, object> compression_params = null, bool? update_on_kvstore = null)
        {
            var paramValues = @params.Values();
            _params = new List<Parameter>();
            var keys = @params.Keys();
            for (var i = 0; i < keys.Length; i++)
            {
                var param = @params[keys[i]];
                _param2idx[keys[i]] = i;
                _params.Add(param);
                param.SetTrainer(this);
                if (param.Stype != StorageStype.Default)
                    _contains_sparse_weight = true;

                if (param.Grad_Stype != StorageStype.Default)
                    _contains_sparse_grad = true;
            }

            _compression_params = compression_params;
            _contexts = CheckContexts();
            InitOptimizer(optimizer);
            _scale = optimizer.RescaleGrad;
            _kvstore_params = new Dictionary<string, object>();
            _kvstore_params.Add("kvstore", kvstore);
            _kvstore_params.Add("update_on_kvstore", update_on_kvstore);
            _kvstore = null;
            _update_on_kvstore = null;
            _params_to_init = new List<Parameter>();
            ResetKVstore();
        }

        public float LearningRate
        {
            get => Optimizer.LearningRate;
            set => Optimizer.SetLearningRate(value);
        }

        public Optimizer Optimizer { get; private set; }

        internal Context[] CheckContexts()
        {
            var contexts = new List<Context>();
            foreach (var param in _params)
            {
                var ctx = param.ListCtx();
                if (contexts.Count == 0) contexts = ctx.ToList();

                if (contexts[0].ToString() != ctx[0].ToString() || contexts.Count != ctx.Count())
                    throw new Exception("All Parameters must be initialized on the same set of contexts, " +
                                        $"but Parameter {param.Name} is initialized on {ctx[0]} while previous Parameters " +
                                        $"are initialized on {contexts[0]}.");

                contexts = ctx.ToList();
            }

            return contexts.ToArray();
        }

        internal void InitOptimizer(Optimizer optimizer)
        {
            Optimizer = optimizer;
            _updaters = new List<Updater>();
            foreach (var item in _contexts) _updaters.Add(new Updater(optimizer));
        }

        internal void InitParams()
        {
            if (!_kv_initialized)
                throw new Exception("Cannot initialize parameters in KVStore " +
                                    "when KVStore is not initialized.");

            var params_to_init = new List<Parameter>();
            if (_kvstore != null)
                foreach (var param in _params_to_init)
                    if (param.deferred_init.HasValue)
                    {
                        params_to_init.Add(param);
                    }
                    else
                    {
                        var param_arrays = param.CheckAndGet(param._data, null);
                        var idx = _param2idx[param.Name];
                        _kvstore.Init(idx.ToString(), param_arrays[0]);
                        if (param.Stype == StorageStype.Default)
                            _kvstore.Pull(idx.ToString(), param_arrays, -idx);
                    }

            _params_to_init = params_to_init;
        }

        internal void ResetKVstore()
        {
            if (_kvstore != null && _kvstore.Type.Contains("dist"))
                throw new Exception("Cannot reset distributed KVStore.");

            _kv_initialized = false;
            _kvstore = null;
            _distributed = null;
            _update_on_kvstore = null;
            _params_to_init = _params.ToList();
        }

        internal void InitKVstore()
        {
            var config = _kvstore_params;
            var update_on_kvstore = false;
            KVStore kvstore = null;
            if (_contains_sparse_weight)
            {
                (kvstore, update_on_kvstore) = Model.CreateSparseKVStore(config["kvstore"].ToString());
                _distributed = kvstore.Type.Contains("dist");
                if (!(bool) config["update_on_kvstore"])
                    throw new Exception("Cannot set update_on_kvstore=False when sparse weights " +
                                        "are present.");
            }
            else if (_contains_sparse_grad)
            {
                var arg_arrays = new NDArrayDict();
                foreach (var param in _params) arg_arrays[param.Name] = param.Data(_contexts[0]);

                (kvstore, _) = Model.CreateKVStore(config["kvstore"].ToString(), _contexts.Length, arg_arrays);
                if (kvstore != null)
                    _distributed = kvstore.Type.Contains("dist");
                else
                    _distributed = false;

                update_on_kvstore = _distributed.Value;
                if (config.ContainsKey("update_on_kvstore"))
                {
                    if ((bool) config["update_on_kvstore"] == false && _distributed.Value)
                        throw new Exception("Cannot set update_on_kvstore=False on dist kvstore " +
                                            "when sparse gradients are present.");

                    update_on_kvstore = (bool) config["update_on_kvstore"];
                }
            }
            else
            {
                var arg_arrays = new NDArrayDict();
                foreach (var param in _params) arg_arrays[param.Name] = param.Data(_contexts[0]);

                (kvstore, update_on_kvstore) =
                    Model.CreateKVStore(config["kvstore"].ToString(), _contexts.Length, arg_arrays);
                if (kvstore != null)
                    _distributed = kvstore.Type.Contains("dist");
                else
                    _distributed = false;

                if (_distributed.Value && kvstore.Type.Contains("async"))
                {
                    update_on_kvstore = true;
                    if (!(bool) config["update_on_kvstore"])
                        throw new Exception("Please set update_on_kvstore=True " +
                                            "when training in async mode.");
                }

                if (config.ContainsKey("update_on_kvstore") && config["update_on_kvstore"] != null)
                    update_on_kvstore = (bool) config["update_on_kvstore"];
            }

            if (kvstore != null)
            {
                if (_compression_params != null)
                    kvstore.SetGradientCompression(_compression_params);

                if (update_on_kvstore)
                    kvstore.SetOptimizer(Optimizer);

                _kvstore = kvstore;
                _update_on_kvstore = update_on_kvstore;
            }
            else
            {
                _kvstore = null;
                _update_on_kvstore = false;
            }

            _kv_initialized = true;
        }

        internal void RowSparsePull(Parameter parameter, NDArrayList @out, NDArray row_id, bool full_idx = false)
        {
            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            var idx = _param2idx[parameter.Name];
            if (full_idx && _kvstore.Type.Contains("dist"))
            {
                if (row_id.Size != @out[0].Shape[0])
                    throw new Exception("row_id size not equal to @out row size");

                _kvstore.Pull(idx.ToString(), @out.ToArray(), -idx, false);
            }
            else
            {
                _kvstore.RowSparsePull(idx.ToString(), @out.ToArray(), -idx, row_id);
            }
        }

        internal void CheckAndRescaleGrad(float scale)
        {
            if (_update_on_kvstore.HasValue && _update_on_kvstore.Value && _distributed.HasValue &&
                _distributed.Value && _kv_initialized)
                if (Optimizer.RescaleGrad != scale)
                    Logger.Warning("Possible change in the `batch_size` from previous " +
                                   "`step` detected. Optimizer gradient normalizing " +
                                   "factor will not change w.r.t new batch_size when " +
                                   "update_on_kvstore=True and when distributed kvstore " +
                                   "is used.");
        }

        public void Step(int batch_size, bool ignore_stale_grad = false)
        {
            var rescale_grad = _scale / batch_size;
            CheckAndRescaleGrad(rescale_grad);

            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            AllReduceGrads();
            _update(ignore_stale_grad);
        }

        public void AllReduceGrads()
        {
            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            if (_kvstore == null && _update_on_kvstore.Value)
                throw new Exception("allreduce_grads() when parameters are updated on kvstore " +
                                    "is not supported. Try setting `update_on_kvstore` " +
                                    "to False when creating trainer.");

            _all_reduce_grads();
        }

        public void _all_reduce_grads()
        {
            if (_kvstore == null)
                return;

            var i = 0;
            foreach (var param in _params)
            {
                if (param.GradReg != OpGradReq.Null)
                {
                    _kvstore.Push(i.ToString(), param.ListGrad(), -i);
                    if (!_update_on_kvstore.Value)
                        _kvstore.Pull(i.ToString(), param.ListGrad(), -i, _distributed.Value);
                }

                i++;
            }
        }

        public void Update(int batch_size, bool ignore_stale_grad = false)
        {
            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            if (_kvstore == null && _update_on_kvstore.Value)
                throw new Exception("update() when parameters are updated on kvstore " +
                                    "is not supported. Try setting `update_on_kvstore` " +
                                    "to False when creating trainer.");

            CheckAndRescaleGrad(_scale / batch_size);
            _update(ignore_stale_grad);
        }

        private void _update(bool ignore_stale_grad = false)
        {
            var updates = new List<(int[], NDArrayList, NDArrayList)>();
            for (var i = 0; i < _params.Count; i++)
            {
                var param = _params[i];
                if (param.GradReg == OpGradReq.Null)
                    continue;

                if (!ignore_stale_grad)
                {
                    var datalist = param.CheckAndGet(param._data, null);
                    foreach (var data in datalist)
                        if (!data.FreshGrad)
                            Logger.Warning(
                                $"Gradient of Parameter `{param.Name}` on context {data.context} has not been updated " +
                                "by backward since last `step`. This could mean a bug in your " +
                                "model that made it only use a subset of the Parameters (Blocks) " +
                                "for this iteration. If you are intentionally only using a subset, " +
                                "call step with ignore_stale_grad=True to suppress this " +
                                "warning and skip updating of Parameters with stale gradient");
                }

                if (_kvstore == null && _update_on_kvstore.Value)
                {
                    if (param.Stype == StorageStype.Default)
                        _kvstore.Pull(i.ToString(), param.ListData(), -i);

                    continue;
                }

                var indices = new List<int>();
                var grads = new NDArrayList();
                var arrays = new NDArrayList();
                updates = param.ListData().Zip(param.ListGrad(), (arr, grad) =>
                {
                    if (!ignore_stale_grad || arr.FreshGrad)
                    {
                        indices.Add(i);
                        grads.Add(grad);
                        arrays.Add(arr);
                        arr.FreshGrad = false;
                    }

                    return (indices.ToArray(), new NDArrayList(grads.ToArray()), new NDArrayList(arrays.ToArray()));
                }).ToList();
            }

            if (!(_kvstore == null && _update_on_kvstore.Value))
                _updaters.Zip(updates, (updater, upd) =>
                {
                    var (i, w, g) = upd;
                    updater.Call(i, g, w);
                    return true;
                });
        }

        public void SaveStates(string fname)
        {
            if (Optimizer == null)
                return;

            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            if (_update_on_kvstore.Value)
            {
                if (_params_to_init == null)
                    throw new Exception("Cannot save trainer states when some " +
                                        "parameters are not yet initialized in kvstore.");

                _kvstore.SaveOptimizerStates(fname, true);
            }
            else
            {
                var state_str = _updaters[0].GetStates(true);
                File.WriteAllText(fname, state_str);
            }
        }

        public void LoadStates(string fname)
        {
            if (!_kv_initialized)
                InitKVstore();

            if (_params_to_init != null)
                InitParams();

            if (_update_on_kvstore.Value)
            {
                _kvstore.LoadOptimizerStates(fname);
                Optimizer = _kvstore._updater.optimizer;
            }
            else
            {
                var state_str = File.ReadAllText(fname);
                foreach (var updater in _updaters)
                {
                    updater.SetStates(state_str);
                    updater.optimizer = _updaters[0].optimizer;
                }

                Optimizer = _updaters[0].optimizer;
            }

            Optimizer.ParamDict = new Dictionary<int, Parameter>();
            for (var i = 0; i < _params.Count; i++) Optimizer.ParamDict.Add(i, _params[i]);
        }
    }
}