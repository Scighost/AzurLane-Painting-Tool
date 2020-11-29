using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace AzurLane_Painting_Tool
{
    class PaintingInfo : INotifyPropertyChanged
    {

        #region 属性修改时通知

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        #endregion

        #region 字段

        public FileInfo pngFile;
        public FileInfo objFile;
        // 图像块映射
        private readonly List<Rectangle[]> mapping = new List<Rectangle[]>();
        private Bitmap oldBitmap;
        private Bitmap newBitmap;

        #endregion


        #region 属性

        // 索引名
        private string _IndexName;
        public string IndexName
        {
            get { return _IndexName; }
            set
            {
                _IndexName = value;
                OnPropertyChanged("IndexName");
            }
        }

        // 立绘名
        private string _TachieName;
        public string PaintingName
        {
            get { return _TachieName; }
            set
            {
                _TachieName = value;
                OnPropertyChanged("TachieName");
            }
        }

        // 是否导入png文件，即texture
        private bool _IsContainsPng;
        public bool IsContainsPng
        {
            get { return _IsContainsPng; }
            set
            {
                _IsContainsPng = value;
                OnPropertyChanged("IsContainsPng");
            }
        }

        // 是否导入obj文件，即mesh
        private bool _IsContainsObj;
        public bool IsContainsObj
        {
            get { return _IsContainsObj; }
            set
            {
                _IsContainsObj = value;
                OnPropertyChanged("IsContainsObj");
            }
        }

        // 预览临时文件位置
        private string _TempPath;
        public string TempPath
        {
            get { return _TempPath; }
            set
            {
                _TempPath = value;
                OnPropertyChanged("TempPath");
            }
        }

        // 是否在列表被选择
        private bool _IsSelected;
        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        // 是否完成解密
        private bool _IsTransformFinished;
        public bool IsTransformFinished
        {
            get { return _IsTransformFinished; }
            set
            {
                _IsTransformFinished = value;
                OnPropertyChanged("IsTransformFinished");
            }
        }

        // 是否导出成功，null为未导出
        private bool? _IsExported;
        public bool? IsExported
        {
            get { return _IsExported; }
            set
            {
                _IsExported = value;
                OnPropertyChanged("IsExported");
            }
        }

        // 立绘像素大小
        private Size _NewBitmapSize;
        public Size NewBitmapSize
        {
            get { return _NewBitmapSize; }
            set
            {
                _NewBitmapSize = value;
                OnPropertyChanged("NewBitmapSize");
            }
        }

        #endregion


        #region 构造函数

        /// <summary>
        /// 构造函数，导入第一个texture或mesh文件时调用
        /// </summary>
        /// <param name="path">文件目录</param>
        /// <param name="indexname">索引名</param>
        /// <param name="paintingName">立绘名</param>
        public PaintingInfo(string path, string indexname, string paintingName)
        {
            if (Path.GetExtension(path) == ".png")
            {
                pngFile = new FileInfo(path);
                IsContainsPng = true;
            }
            else if (Path.GetExtension(path) == ".obj")
            {
                objFile = new FileInfo(path);
                IsContainsObj = true;
            }
            IndexName = indexname;
            PaintingName = paintingName;
            TempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurlanePaintingTool", $"{PaintingName}.png");
        }

        #endregion


        #region 文件相关操作

        /// <summary>
        /// 已存在texture或mesh文件时，导入另一个
        /// </summary>
        /// <param name="path"></param>
        public void AddFile(string path)
        {
            if (Path.GetExtension(path) == ".png")
            {
                pngFile = new FileInfo(path);
                IsContainsPng = true;
            }
            else if (Path.GetExtension(path) == ".obj")
            {
                objFile = new FileInfo(path);
                IsContainsObj = true;
            }
        }

        /// <summary>
        /// 导出立绘文件
        /// </summary>
        /// <param name="savePath">保存目录</param>
        public void ExportFile(string savePath)
        {
            try
            {
                if (File.Exists(TempPath))
                {
                    File.Copy(TempPath, Path.Combine(savePath, Path.GetFileName(TempPath)));
                    IsExported = true;
                }
                else if (IsContainsPng && _IsContainsObj)
                {
                    oldBitmap = new Bitmap(pngFile.FullName);
                    GetMapping();
                    Transform();
                    newBitmap.Save(Path.Combine(savePath, Path.GetFileName(TempPath)), ImageFormat.Png);
                    oldBitmap.Dispose();
                    newBitmap.Dispose();
                    IsExported = true;
                }
                else
                {
                    IsExported = false;
                }
            }
            catch
            {
                IsExported = false;
            }
        }

        #endregion


        #region 立绘解密相关操作

        /// <summary>
        /// 开始解密，并保存临时文件，若已有临时文件，则跳过
        /// </summary>
        public void GoStart()
        {
            if (IsContainsPng && _IsContainsObj)
            {
                if (!File.Exists(TempPath))
                {
                    oldBitmap = new Bitmap(pngFile.FullName);
                    GetMapping();
                    Transform();
                    newBitmap.Save(TempPath, ImageFormat.Png);
                    oldBitmap.Dispose();
                    newBitmap.Dispose();
                    GC.Collect();
                }
                IsTransformFinished = true;
            }
        }

        /// <summary>
        /// 获取立绘图片块映射
        /// </summary>
        public void GetMapping()
        {
            var stream = new StreamReader(objFile.FullName, Encoding.UTF8);
            var vsBuffer = new List<int[]>();
            var vtBuffer = new List<double[]>();
            string buffer;
            string[] splitBuffer;
            for (int i = 0; ; i++)
            {
                buffer = stream.ReadLine();
                splitBuffer = buffer.Split(' ');
                if (splitBuffer[0] == "v") // 以v开头的数据和新文件图像块的左上角位置有关
                {
                    vsBuffer.Add(new int[] { -int.Parse(splitBuffer[1]), int.Parse(splitBuffer[2]) });
                }
                if (splitBuffer[0] == "vt") // 以vt开头的数据和老文件的图像块有关
                {
                    vtBuffer.Add(new double[] { double.Parse(splitBuffer[1]), double.Parse(splitBuffer[2]) });
                }
                if (splitBuffer[0] == "g" && i != 0) // 文件中以g开头的数据没有用
                {
                    break;
                }
            }
            stream.Close();
            int[][] v = vsBuffer.ToArray();
            double[][] vt = vtBuffer.ToArray();
            var temp = new int[6];
            // 每4行一组，获取图像块映射
            for (int i = 0; i < v.Length; i += 4)
            {
                // tmp的6个数分别为图像块的像素位置：老图左、老图上、老图右、老图下、新图左、新图右
                // 老图数据加密时经过了归一化处理，需四舍五入防止出现空白行或空白列
                temp[0] = (int)Math.Round((Min(vt[i][0], vt[i + 1][0], vt[i + 2][0]) * oldBitmap.Width));
                temp[1] = (int)Math.Round((Min(vt[i][1], vt[i + 1][1], vt[i + 2][1]) * oldBitmap.Height));
                temp[2] = (int)Math.Round((Max(vt[i][0], vt[i + 1][0], vt[i + 2][0]) * oldBitmap.Width));
                temp[3] = (int)Math.Round((Max(vt[i][1], vt[i + 1][1], vt[i + 2][1]) * oldBitmap.Height));
                temp[4] = (int)Math.Round(Min(v[i][0], v[i + 1][0], v[i + 2][0]));
                temp[5] = (int)Math.Round(Min(v[i][1], v[i + 1][1], v[i + 2][1]));
                // 添加图像块映射
                mapping.Add(new Rectangle[] { new Rectangle(temp[0], temp[1], temp[2] - temp[0], temp[3] - temp[1]),
                                              new Rectangle(temp[4], temp[5], temp[2] - temp[0], temp[3] - temp[1]) });
            }
        }

        /// <summary>
        /// 根据映射解密立绘
        /// </summary>
        public void Transform()
        {
            // 设置立绘图像像素大小
            int width = 0;
            int height = 0;
            mapping.ForEach(x =>
            {
                width = width > x[1].Right ? width : x[1].Right;
                height = height > x[1].Bottom ? height : x[1].Bottom;
            });
            // 上下翻转老图
            oldBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            newBitmap = new Bitmap(width, height, oldBitmap.PixelFormat);
            NewBitmapSize = newBitmap.Size;
            // 设置图像分辨率
            newBitmap.SetResolution(oldBitmap.HorizontalResolution, oldBitmap.VerticalResolution);
            BitmapData oldBitmapData;
            BitmapData newBitmapData;
            // 复制图像块
            foreach (var item in mapping)
            {
                oldBitmapData = oldBitmap.LockBits(item[0], ImageLockMode.ReadOnly, oldBitmap.PixelFormat);
                newBitmapData = newBitmap.LockBits(item[1], ImageLockMode.WriteOnly, oldBitmap.PixelFormat);
                unsafe
                {
                    byte* ptrOld = (byte*)oldBitmapData.Scan0;
                    byte* ptrNew = (byte*)newBitmapData.Scan0;
                    int oldOffset = oldBitmapData.Stride - item[0].Width * 4;
                    int newOffset = newBitmapData.Stride - item[1].Width * 4;
                    for (int i = 0; i < item[0].Height; i++)
                    {
                        for (int j = 0; j < item[0].Width; j++)
                        {
                            ptrNew[0] = ptrOld[0];
                            ptrNew[1] = ptrOld[1];
                            ptrNew[2] = ptrOld[2];
                            ptrNew[3] = ptrOld[3];
                            ptrOld += 4;
                            ptrNew += 4;
                        }
                        ptrOld += oldOffset;
                        ptrNew += newOffset;
                    }
                }
                oldBitmap.UnlockBits(oldBitmapData);
                newBitmap.UnlockBits(newBitmapData);
            }
            // 翻转新图
            newBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        // 三者最大值
        private static readonly Func<double, double, double, double> Max = (a, b, c) => Math.Max(Math.Max(a, b), Math.Max(a, c));

        // 三者最小值
        private static readonly Func<double, double, double, double> Min = (a, b, c) => Math.Min(Math.Min(a, b), Math.Min(a, c));


        #endregion

    }
}