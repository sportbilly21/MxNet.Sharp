﻿using MxNet.Gluon;
using System;
using System.Collections.Generic;
using System.Text;

namespace MxNet.GluonCV.NN
{
    public class CompositeMatcher : HybridBlock
    {
        public CompositeMatcher(Block[] matchers, string prefix = null, ParameterDict @params = null) : base(prefix, @params)
        {
            throw new NotImplementedException();
        }

        public override NDArrayOrSymbol HybridForward(NDArrayOrSymbol x, params NDArrayOrSymbol[] args)
        {
            throw new NotImplementedException();
        }

        private NDArray ComposeMatches(NDArrayList matches)
        {
            throw new NotImplementedException();
        }
    }
}
