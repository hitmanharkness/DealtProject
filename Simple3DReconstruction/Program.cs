//----------------------------------------------------------------------------
//  Copyright (C) 2004-2017 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Emgu.CV;
using System.Drawing;
using Emgu.CV.Structure;
using System.Diagnostics;
using Emgu.CV.CvEnum;
using System.IO.Ports;

namespace Simlpe3DReconstruction
{

    // This came from stackoverflow.
// MAT:
// https://stackoverflow.com/questions/32255440/how-can-i-get-and-set-pixel-values-of-an-emgucv-mat-image


    class Program
   {
        static void Main(string[] args)
        {
            MCvPoint3D32f[] _points;
            Mat _left = CvInvoke.Imread("imL.png", ImreadModes.Color);
            Mat _right = CvInvoke.Imread("imR.png", ImreadModes.Color);
            Mat disparityMap = new Mat();

            Stopwatch watch = Stopwatch.StartNew();
            UMat leftGray = new UMat();
            UMat rightGray = new UMat();
            CvInvoke.CvtColor(_left, leftGray, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(_right, rightGray, ColorConversion.Bgr2Gray);
            Mat points = new Mat();
            Computer3DPointsFromStereoPair(leftGray, rightGray, disparityMap, points);
            watch.Stop();
            long disparityComputationTime = watch.ElapsedMilliseconds;

            Mat pointsArray = points.Reshape(points.NumberOfChannels, points.Rows * points.Cols);
            Mat colorArray = _left.Reshape(_left.NumberOfChannels, _left.Rows * _left.Cols);
            Mat colorArrayFloat = new Mat();
            colorArray.ConvertTo(colorArrayFloat, DepthType.Cv32F);
            WCloud cloud = new WCloud(pointsArray, colorArray);

            // My attempt to grab a pixel.
            Image<Bgr, Byte> image = disparityMap.ToImage<Bgr, Byte>();

            int threshhold = 190;
            bool objectFound = false;

            for (int i = 0; i < image.Rows; i++) { 
                for (int j = 0; j < disparityMap.Cols; j++)
                {
                    // If it's below the threshhold black it out.
                    if (image.Data[i, j, 0] < threshhold)
                    {
                        image.Data[i, j, 2] = 0;
                        image.Data[i, j, 1] = 0;
                        image.Data[i, j, 0] = 0;
                    }
                    else
                    {
                        objectFound = true;
                        Console.Write("(" + image.Data[i, j, 0].ToString().PadLeft(3, '0') + ","); // Blue;
                        Console.Write(image.Data[i, j, 1].ToString().PadLeft(3, '0') + ","); // Green;
                        Console.Write(image.Data[i, j, 2].ToString().PadLeft(3, '0') + ")"); // Red;
                    }
                }
                Console.WriteLine(); // new row end line;
            }

            // 2D Disparity Display
            Mat show = new Mat();

            disparityMap.ConvertTo(show, DepthType.Cv8U);
            CvInvoke.Imshow("Disparity", show);

            disparityMap.ConvertTo(show, DepthType.Cv8U);
            CvInvoke.Imshow("Disparity Restricted", image);


            //try
            //{
            //    if (objectFound)
            //    {
            //        SerialPort serialPort1 = new SerialPort("COM6", 9600);
            //        serialPort1.Open();
            //        serialPort1.WriteLine("");
            //        System.Threading.Thread.Sleep(1000);
            //        serialPort1.WriteLine("");
            //        serialPort1.Close();
            //    }
            //}
            //catch (Exception ex)
            //{

            //}
            // 3D IMAGE DISPLAY
            Emgu.CV.Viz3d v = new Emgu.CV.Viz3d("Simple stereo reconstruction");
            WText wtext = new WText("3d point cloud", new System.Drawing.Point(20, 20), 20, new MCvScalar(255, 255, 255));
            WCoordinateSystem wCoordinate = new WCoordinateSystem(1.0);
            v.ShowWidget("text", wtext);
            //v.ShowWidget("coordinate", wCoordinate);
            v.ShowWidget("cloud", cloud);
            v.Spin();

            CvInvoke.WaitKey(0);


        }


      /// <summary>
      /// Given the left and right image, computer the disparity map and the 3D point cloud.
      /// </summary>
      /// <param name="left">The left image</param>
      /// <param name="right">The right image</param>
      /// <param name="outputDisparityMap">The left disparity map</param>
      /// <param name="points">The 3D point cloud within a [-0.5, 0.5] cube</param>
      private static void Computer3DPointsFromStereoPair(IInputArray left, IInputArray right, Mat outputDisparityMap, Mat points)
      {
         Size size;
         using (InputArray ia = left.GetInputArray())
            size = ia.GetSize();

         using (StereoBM stereoSolver = new StereoBM())
         {
            stereoSolver.Compute(left, right, outputDisparityMap);

            float scale = Math.Max(size.Width, size.Height);

            //Construct a simple Q matrix, if you have a matrix from cvStereoRectify, you should use that instead
            using (Matrix<double> q = new Matrix<double>(
               new double[,]
               {
                  {1.0, 0.0, 0.0, -size.Width/2}, //shift the x origin to image center
                  {0.0, -1.0, 0.0, size.Height/2}, //shift the y origin to image center and flip it upside down
                  {0.0, 0.0, -1.0, 0.0}, //Multiply the z value by -1.0, 
                  {0.0, 0.0, 0.0, scale}
               })) //scale the object's coordinate to within a [-0.5, 0.5] cube
            {
               
               CvInvoke.ReprojectImageTo3D(outputDisparityMap, points, q, false, DepthType.Cv32F);
               
            }
            //points = PointCollection.ReprojectImageTo3D(outputDisparityMap, q);
         }
      }
   }
}