using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                // Input directory or file path and get matched list.
                List<ValueTuple<string, Bitmap, string>> matches = InputAndGetMatches(out string savePath);
                Console.Write("Waiting...");
                // Get list of mapping about original and transformed image.
                var mapping = new List<ValueTuple<List<Rectangle[]>, Bitmap, string>>();
                matches.ForEach(x => mapping.Add((GetMapping(x.Item1, x.Item2), x.Item2, x.Item3)));
                Directory.CreateDirectory(savePath);
                // Transform image and save.
                mapping.ForEach(x => Transform(x.Item1, x.Item2).Save(savePath + "\\" + x.Item3, ImageFormat.Png));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine("Files are saved in {0}\n", savePath);
            }
        }

        /// <summary>
        /// Input directory or file path, return matched list of ValueTuple(mesh file's path, png file's Bitmap Class, file name) and save path.  
        /// </summary>
        /// <param name="savePath">transformed files' save path</param>
        /// <typeparam name="savePath">out string</typeparam>
        /// <returns>list of ValueTuple(mesh file's path, png file's Bitmap Class, file name)</returns>
        static List<ValueTuple<string, Bitmap, string>> InputAndGetMatches(out string savePath)
        {
            savePath = null;
        restart:
            Console.WriteLine("Please input directory path or file path:");
            string input = Console.ReadLine().Replace("\"", "");
            input = input == "" ? Environment.CurrentDirectory : input;
            var list = new List<ValueTuple<string, Bitmap, string>>();
            if (File.Exists(input))
            {
                // If input a file path.
                string name;
                var file = new FileInfo(input);
                // Remove "-mesh.obj" and ".png".
                name = file.Name.Replace("-mesh.obj", "").Replace(".png", "");
                DirectoryInfo fileDir = file.Directory;
                FileInfo[] fileInfos = fileDir.GetFiles();
                fileInfos = Array.FindAll(fileInfos, x => x.Name.Replace("-mesh.obj", "").Replace(".png", "") == name);
                savePath = fileDir.FullName;
                if (fileInfos.Length == 1)
                {
                    // If can't find related file, search it from ../Mesh or ../Texture2D.
                    string rootPath = fileDir.Parent.FullName;
                    savePath = rootPath;
                    try
                    {
                        fileDir = new DirectoryInfo(rootPath + "\\Mesh");
                        fileInfos = fileDir.GetFiles();
                        fileInfos = Array.FindAll(fileInfos, x => x.Name.Replace("-mesh.obj", "").Replace(".png", "") == name);
                        FileInfo temp = fileInfos[0];
                        fileDir = new DirectoryInfo(rootPath + "\\Texture2D");
                        fileInfos = fileDir.GetFiles();
                        fileInfos = Array.FindAll(fileInfos, x => x.Name.Replace("-mesh.obj", "").Replace(".png", "") == name);
                        fileInfos = new FileInfo[] { fileInfos[0], temp };
                    }
                    catch { }
                }
                if (fileInfos.Length == 1)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    switch (file.Extension)
                    {
                        case ".obj":
                            Console.WriteLine("Can't find the png file.\n");
                            goto restart;
                        case ".png":
                            Console.WriteLine("Can't find the obj file.\n");
                            goto restart;
                        default:
                            goto restart;
                    }
                }
                else
                {
                    list.Add(fileInfos[0].Extension == ".obj" ? (fileInfos[0].FullName, new Bitmap(fileInfos[1].FullName), name + ".png") : (fileInfos[1].FullName, new Bitmap(fileInfos[0].FullName), name + ".png"));
                }
            }
            if (Directory.Exists(input))
            {
                // If input a directory path.
                savePath = input;
                var directory = new DirectoryInfo(input);
                FileInfo[] files = directory.GetFiles();
                var objs = Array.FindAll(files, x => x.Name.Contains("-mesh.obj"));
                var pngs = Array.FindAll(files, x => x.Extension == ".png");
                if (objs.Length == 0 || pngs.Length == 0)
                {
                    // If can't find related file, search it from /Mesh or /Texture2D.
                    if (Directory.Exists(input + "\\Mesh") && Directory.Exists(input + "\\Texture2D"))
                    {
                        directory = new DirectoryInfo(input + "\\Mesh");
                        files = directory.GetFiles();
                        objs = Array.FindAll(files, x => x.Extension == ".obj");
                        directory = new DirectoryInfo(input + "\\Texture2D");
                        files = directory.GetFiles();
                        pngs = Array.FindAll(files, x => x.Extension == ".png");
                    }
                    else
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.WriteLine("Can't find related files.\n");
                        goto restart;
                    }
                }
                try
                {
                    Array.ForEach(objs, x =>
                    list.Add((x.FullName, new Bitmap(Array.Find(pngs, y => y.Name[0..^4] == x.Name[0..^9]).FullName), x.Name.Replace("-mesh.obj", ".png"))));
                }
                catch { }
            }
            savePath += "\\Paintings";
            return list;
        }

        /// <summary>
        /// Get image block mapping of initial png file and transformed png file.
        /// </summary>
        /// <param name="meshPath">mesh file path</param>
        /// <param name="src">initial image's Bitmap Class</param>
        /// <returns>related mapping, list of Rectangle[] Class</returns>
        static List<Rectangle[]> GetMapping(string meshPath, Bitmap src)
        {
            var mapping = new List<Rectangle[]>();
            var stream = new StreamReader(meshPath, Encoding.UTF8);
            var vsBuffer = new List<int[]>();
            var vtBuffer = new List<double[]>();
            string buffer;
            string[] splitBuffer;
            for (int i = 0; ; i++)
            {
                // Classify text block according to the first word of each line.
                buffer = stream.ReadLine();
                splitBuffer = buffer.Split(" ");
                if (splitBuffer[0] == "v")
                {
                    vsBuffer.Add(new int[] { -int.Parse(splitBuffer[1]), int.Parse(splitBuffer[2]) });
                }
                if (splitBuffer[0] == "vt")
                {
                    vtBuffer.Add(new double[] { double.Parse(splitBuffer[1]), double.Parse(splitBuffer[2]) });
                }
                if (splitBuffer[0] == "g" && i != 0)
                {
                    break;
                }
            }
            stream.Close();
            int[][] v = vsBuffer.ToArray();
            double[][] vt = vtBuffer.ToArray();
            var picMapping = new List<Rectangle[]>();
            // information of original and targeted image's mapping rectangle.
            var temp = new int[6];
            for (int i = 0; i < v.Length; i += 4)
            {
                // temp(original left, original top, original right, original botton, targeted left, target top).
                temp[0] = (int)Math.Round((Min(vt[i][0], vt[i + 1][0], vt[i + 2][0]) * src.Width));
                temp[1] = (int)Math.Round((Min(vt[i][1], vt[i + 1][1], vt[i + 2][1]) * src.Height));
                temp[2] = (int)Math.Round((Max(vt[i][0], vt[i + 1][0], vt[i + 2][0]) * src.Width));
                temp[3] = (int)Math.Round((Max(vt[i][1], vt[i + 1][1], vt[i + 2][1]) * src.Height));
                temp[4] = (int)Math.Round(Min(v[i][0], v[i + 1][0], v[i + 2][0]));
                temp[5] = (int)Math.Round(Min(v[i][1], v[i + 1][1], v[i + 2][1]));
                mapping.Add(new Rectangle[] { new Rectangle(temp[0], temp[1], temp[2] - temp[0], temp[3] - temp[1]), new Rectangle(temp[4], temp[5], temp[2] - temp[0], temp[3] - temp[1]) });
            }
            return mapping;
        }

        /// <summary>
        /// Get transformed image from mapping and original image.
        /// </summary>
        /// <param name="mapping">mapping of initial png file and transformed png file</param>
        /// <param name="src">original image's Bitmap Class</param>
        /// <returns>transformed image's Bitmap Class</returns>
        static Bitmap Transform(List<Rectangle[]> mapping, Bitmap src)
        {
            int width = 0;
            int height = 0;
            // size of transformed image.
            mapping.ForEach(x =>
            {
                width = width > x[1].Right ? width : x[1].Right;
                height = height > x[1].Bottom ? height : x[1].Bottom;
            });
            src.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var target = new Bitmap(width, height);
            target.SetResolution(src.HorizontalResolution, src.VerticalResolution);
            Graphics graphics = Graphics.FromImage(target);
            // seem to Ctrl+C and Ctrl+V.
            mapping.ForEach(x =>
            {
                // exclude zero-sized block
                if (x[0].Width > 0 && x[0].Height > 0)
                    graphics.DrawImage(src.Clone(x[0], src.PixelFormat), x[1]);
            });
            target.RotateFlip(RotateFlipType.RotateNoneFlipY);
            graphics.Dispose();
            src.Dispose();
            return target;
        }

        // the maximum of 3 numbers.
        static Func<double, double, double, double> Max = (a, b, c) => Math.Max(Math.Max(a, b), Math.Max(a, c));

        // the minimum of 3 numbers.
        static Func<double, double, double, double> Min = (a, b, c) => Math.Min(Math.Min(a, b), Math.Min(a, c));
    }
}