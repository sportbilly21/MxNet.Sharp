﻿/*****************************************************************************
   Copyright 2018 The MxNet.Sharp Authors. All Rights Reserved.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
******************************************************************************/
namespace MxNet.Optimizers
{
    public class Ftlr : Optimizer
    {
        public Ftlr(float lamda1 = 0.1f, float learning_rate = 0.1f, float beta = 1) : base(
            learning_rate: learning_rate)
        {
            Lamda1 = lamda1;
            Beta = beta;
        }

        public float Lamda1 { get; set; }

        public float Beta { get; set; }

        public override NDArrayDict CreateState(int index, NDArray weight)
        {
            var state = new NDArrayDict();
            state["z"] = nd.Zeros(weight.Shape, weight.Context, weight.DataType);
            state["n"] = nd.Zeros(weight.Shape, weight.Context, weight.DataType);
            return state;
        }

        public override void Update(int index, NDArray weight, NDArray grad, NDArrayDict state)
        {
            UpdateCount(index);
            var lr = GetLr(index);
            var wd = GetWd(index);
            var t = index_update_count[index];
            weight = nd.FtrlUpdate(weight, grad, state["z"], state["n"], lr, Lamda1, Beta, wd, RescaleGrad,
                ClipGradient.HasValue ? ClipGradient.Value : -1);
        }
    }
}