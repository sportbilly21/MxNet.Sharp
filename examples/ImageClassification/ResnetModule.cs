﻿using MxNet;
using MxNet.Gluon.ModelZoo.Vision;
using MxNet.Image;
using MxNet.IO;
using MxNet.Modules;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ImageClassification
{
    public class ResnetModule
    {

        public static void Run()
        {
            string resnet50symbolUrl = "http://data.mxnet.io.s3-website-us-west-1.amazonaws.com/models/imagenet/resnet/50-layers/resnet-50-symbol.json";
            string resnet50paramsUrl = "http://data.mxnet.io.s3-website-us-west-1.amazonaws.com/models/imagenet/resnet/50-layers/resnet-50-0000.params";

            TestUtils.Download(resnet50symbolUrl);
            TestUtils.Download(resnet50paramsUrl);

            string testimg = "goldfish.jpg";
            var imgbytes = File.ReadAllBytes(testimg);
            var array = prepareNDArray(imgbytes);
            var model = LoadModel("resnet-50", gpu: true);
            var prob = model.Predict(array);
            var predictIndexes = nd.Softmax(prob).Topk(k: 5).AsArray<float>().OfType<float>().ToList();
            var imagenet_labels = TestUtils.GetImagenetLabels();
            foreach (int i in predictIndexes)
            {
                Console.WriteLine(imagenet_labels[i]);
            }
        }

        private static Module LoadModel(string prefix, int epoch = 0, bool gpu = true)
        {
            var (sym, arg_params, aux_params) = Model.LoadCheckpoint(prefix, epoch);
            arg_params["prob_label"] = new NDArray(new float[0]);
            arg_params["softmax_label"] = new NDArray(new float[0]);
            Module mod = null;
            if (gpu)
                mod = new Module(symbol: sym, context: new[] { mx.Gpu(0) }, data_names: new string[] { "data" });
            else
                mod = new Module(symbol: sym, data_names: new string[] { "data" });

            mod.Bind(for_training: false, data_shapes: new[] { new DataDesc("data", new Shape(1, 3, 224, 224)) });
            mod.SetParams(arg_params, aux_params);
            return mod;
        }

        private static NDArray prepareNDArray(byte[] image)
        {
            var img  = Cv2.ImDecode(image, ImreadModes.Color);
            Cv2.Resize(img, img, new OpenCvSharp.Size(224, 224));
            var x = NDArray.LoadCV2Mat(img);
            x = x.Transpose(new Shape(2, 0, 1));
            x = x.ExpandDims(axis: 0);

            return x;
        }
    }
}
