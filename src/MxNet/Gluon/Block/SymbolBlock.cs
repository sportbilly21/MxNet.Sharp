﻿using System;

namespace MxNet.Gluon
{
    public class SymbolBlock : HybridBlock
    {
        public SymbolBlock(Symbol[] outputs, Symbol[] inputs, ParameterDict @params = null)
        {
            throw new NotImplementedException();
        }

        public override NDArrayOrSymbol HybridForward(NDArrayOrSymbol x, params NDArrayOrSymbol[] args)
        {
            throw new NotImplementedException();
        }

        public override NDArrayOrSymbol Forward(NDArrayOrSymbol input, NDArrayOrSymbol[] args)
        {
            throw new NotImplementedException();
        }

        public static SymbolBlock Imports(string symbol_file, string[] input_names, string param_file = null,
            Context ctx = null)
        {
            throw new NotImplementedException();
        }

        private void ClearCachedOp()
        {
            throw new NotImplementedException();
        }

        public override void Cast(DType dtype)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}