﻿using MxNet.Gluon;
using System;
using System.Collections.Generic;
using System.Text;

namespace MxNet.GluonCV.NN
{
    public class HardSwish : HybridBlock
    {
        public HardSwish(string prefix = null, ParameterDict @params = null) : base(prefix, @params)
        {
        }

        public override NDArrayOrSymbol HybridForward(NDArrayOrSymbol x, params NDArrayOrSymbol[] args)
        {
            throw new NotImplementedException();
        }
    }
}
