﻿using MxNet.Initializers;

namespace MxNet.Gluon.NN
{
    public class Conv1DTranspose : _Conv
    {
        public Conv1DTranspose(int channels, int kernel_size, int strides = 1, int padding = 0, int output_padding = 0,
            int dilation = 1, int groups = 1, string layout = "NCW",
            ActivationActType? activation = null, bool use_bias = true, Initializer weight_initializer = null,
            string bias_initializer = "zeros", int in_channels = 0, string prefix = null, ParameterDict @params = null)
            : base(channels, new[] {kernel_size}, new[] {strides}, new[] {padding},
                new[] {dilation}, groups, layout, in_channels, activation, use_bias,
                weight_initializer, bias_initializer, null, "Convolution", prefix, @params)
        {
        }
    }
}