﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace MxNet.Gluon.RNN
{
    public class BidirectionalCell : HybridRecurrentCell
    {
        private readonly string _output_prefix;

        public BidirectionalCell(RecurrentCell l_cell, RecurrentCell r_cell, string output_prefix = "bi_") : base("",
            null)
        {
            RegisterChild(l_cell, "l_cell");
            RegisterChild(r_cell, "r_cell");
            _output_prefix = output_prefix;
        }

        public (Symbol, List<Symbol[]>) Call(Symbol inputs, List<Symbol[]> states)
        {
            throw new NotSupportedException("Bidirectional cannot be stepped. Please use unroll");
        }

        public override StateInfo[] StateInfo(int batch_size = 0)
        {
            return RNNCell.CellsStateInfo(_childrens.Values.ToArray(), batch_size);
        }

        public override NDArrayOrSymbol[] BeginState(int batch_size = 0, string func = null, FuncArgs args = null)
        {
            return RNNCell.CellsBeginState(_childrens.Values.ToArray(), batch_size, func);
        }

        public override (NDArrayOrSymbol[], NDArrayOrSymbol[]) Unroll(int length, NDArrayOrSymbol[] inputs,
            NDArrayOrSymbol[] begin_state = null, string layout = "NTC", bool? merge_outputs = null,
            Symbol valid_length = null)
        {
            Reset();
            var axis = 0;
            var batch_size = 0;
            (inputs, axis, batch_size) = RNNCell.FormatSequence(length, inputs, layout, false);
            var reversed_inputs = RNNCell._reverse_sequences(inputs, length, valid_length);
            begin_state = RNNCell.GetBeginState(this, begin_state, inputs, batch_size);
            var states = begin_state.ToList();
            var l_cell = _childrens["l_cell"];
            var r_cell = _childrens["r_cell"];

            var (l_outputs, l_states) = l_cell.Unroll(length, inputs, states.Take(l_cell.StateInfo().Length).ToArray(),
                layout, merge_outputs, valid_length);
            var (r_outputs, r_states) = r_cell.Unroll(length, inputs, states.Skip(l_cell.StateInfo().Length).ToArray(),
                layout, merge_outputs, valid_length);

            var reversed_r_outputs = RNNCell._reverse_sequences(r_outputs, length, valid_length);
            if (!merge_outputs.HasValue)
            {
                merge_outputs = l_outputs.Length > 1;

                (l_outputs, _, _) = RNNCell.FormatSequence(null, l_outputs, layout, merge_outputs.Value);
                (reversed_r_outputs, _, _) =
                    RNNCell.FormatSequence(null, reversed_r_outputs, layout, merge_outputs.Value);
            }

            NDArrayOrSymbol[] outputs = null;
            if (merge_outputs.Value)
            {
                if (reversed_r_outputs[0].IsNDArray)
                    reversed_r_outputs = new NDArrayOrSymbol[]
                        {nd.Stack(reversed_r_outputs.ToList().ToNDArrays(), reversed_r_outputs.Length, axis)};
                else
                    reversed_r_outputs = new NDArrayOrSymbol[]
                        {sym.Stack(reversed_r_outputs.ToList().ToSymbols(), reversed_r_outputs.Length, axis)};

                var concatList = l_outputs.ToList();
                concatList.AddRange(reversed_r_outputs);
                if (reversed_r_outputs[0].IsNDArray)
                    outputs = new NDArrayOrSymbol[] {nd.Concat(concatList.ToList().ToNDArrays(), 2)};
                else
                    outputs = new NDArrayOrSymbol[] {sym.Concat(concatList.ToList().ToSymbols(), 2)};
            }
            else
            {
                var outputs_temp = new List<NDArrayOrSymbol>();
                for (var i = 0; i < l_outputs.Length; i++)
                {
                    var l_o = l_outputs[i];
                    var r_o = reversed_r_outputs[i];
                    if (l_o.IsNDArray)
                        outputs_temp.Add(nd.Concat(new NDArray[] {l_o, r_o}));
                    else
                        outputs_temp.Add(sym.Concat(new Symbol[] {l_o, r_o}, 1, symbol_name: $"{_output_prefix}t{i}"));
                }

                outputs = outputs_temp.ToArray();
                outputs_temp.Clear();
            }


            if (valid_length != null)
                outputs = RNNCell.MaskSequenceVariableLength(outputs, length, valid_length, axis, merge_outputs.Value);

            states.Clear();
            states.AddRange(l_states);
            states.AddRange(r_states);

            return (outputs, states.ToArray());
        }

        public override (NDArrayOrSymbol, NDArrayOrSymbol[]) HybridForward(NDArrayOrSymbol x,
            params NDArrayOrSymbol[] args)
        {
            return default;
        }
    }
}