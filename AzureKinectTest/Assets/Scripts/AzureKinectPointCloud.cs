using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Microsoft.Azure.Kinect.Sensor;
using System;

[RequireComponent(typeof(MeshFilter)),RequireComponent(typeof(MeshRenderer))]
public class AzureKinectPointCloud : MonoBehaviour
{
    [SerializeField] private int deviceID = 0;
    [SerializeField] private MeshType meshType = MeshType.PointCloud;
    [Range(0.001f, 0.01f), SerializeField] private float pointSize = 0.005f;

    private Device _kinectDevice = null;
    private bool isRunning = false;

    [Serializable]
    public enum MeshType
    {
        PointCloud,
        TriangleMesh
    }

    // Start is called before the first frame update
    void Start() => _ = KinectUpdate();

    private async Task KinectUpdate()
    {
        // Azure Kinect の起動
        _kinectDevice = Device.Open(deviceID);
        _kinectDevice.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_2x2Binned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30
        });
        var transformation = _kinectDevice.GetCalibration().CreateTransformation();
        var width = _kinectDevice.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        var height = _kinectDevice.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        var depthImage = new Image(ImageFormat.ColorBGRA32, width, height, width * 4);

        // PointCloud 表示用メッシュ設定
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var vertex = new Vector3[width * height * 3];
        var color = new Color32[width * height * 3];
        var index = new List<int>();
        GetComponent<MeshFilter>().mesh = mesh;

        // PointCloud更新
        isRunning = true;
        while (isRunning)
        {
            using (var capture = await Task.Run(() => _kinectDevice.GetCapture()).ConfigureAwait(true))
            {
                transformation.ColorImageToDepthCamera(capture, depthImage);
                var pointCloudImage = transformation.DepthImageToPointCloud(capture.Depth);
                var colorArray = depthImage.GetPixels<BGRA>().ToArray();
                var depthArray = pointCloudImage.GetPixels<Short3>().ToArray();
                GetMeshData(ref vertex, ref color,ref index, width, height, depthArray, colorArray);
                mesh.SetVertices(vertex);
                mesh.SetColors(color);
                mesh.SetIndices(index, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();
            }
        }

        // 解放処理
        mesh.Clear();
    }

    void OnApplicationQuit()
    {
        // Azure Kinect停止
        isRunning = false;
        _kinectDevice?.StopCameras();
        _kinectDevice?.Dispose();
    }

    // 頂点、カラー、頂点順番の設定
    private void GetMeshData(ref Vector3[] vertex,ref Color32[] color, ref List<int> indexList, int width,int height,in Short3[] depthArray, in BGRA[] colorArray)
    {
        indexList.Clear();
        if (meshType == MeshType.PointCloud)
        {
            // 点群表示のメッシュ設定
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    vertex[i * width * 3 + j * 3 + 0].x = depthArray[i * width + j].X * 0.001f;
                    vertex[i * width * 3 + j * 3 + 0].y = -depthArray[i * width + j].Y * 0.001f + pointSize;//上下反転
                    vertex[i * width * 3 + j * 3 + 0].z = depthArray[i * width + j].Z * 0.001f;

                    vertex[i * width * 3 + j * 3 + 1].x = depthArray[i * width + j].X * 0.001f + pointSize;
                    vertex[i * width * 3 + j * 3 + 1].y = -depthArray[i * width + j].Y * 0.001f - pointSize;//上下反転
                    vertex[i * width * 3 + j * 3 + 1].z = depthArray[i * width + j].Z * 0.001f;

                    vertex[i * width * 3 + j * 3 + 2].x = depthArray[i * width + j].X * 0.001f - pointSize;
                    vertex[i * width * 3 + j * 3 + 2].y = -depthArray[i * width + j].Y * 0.001f - pointSize;//上下反転
                    vertex[i * width * 3 + j * 3 + 2].z = depthArray[i * width + j].Z * 0.001f;

                    for (int k = 0; k < 3; k++)
                    {
                        color[i * width * 3 + j * 3 + k].b = colorArray[i * width + j].B;
                        color[i * width * 3 + j * 3 + k].g = colorArray[i * width + j].G;
                        color[i * width * 3 + j * 3 + k].r = colorArray[i * width + j].R;
                        color[i * width * 3 + j * 3 + k].a = 255;
                    }
                }
            }
            for (int i = 0; i < width * height; i++)
            {
                var p = i * 3;
                bool isVaridPoint = vertex[p].magnitude != 0;
                if (isVaridPoint)
                {
                    indexList.Add(p + 0);
                    indexList.Add(p + 1);
                    indexList.Add(p + 2);
                }
            }
        }
        else
        {
            // 簡易メッシュ表示のメッシュ設定
            for (int i = 0; i < width * height; i++)
            {
                vertex[i].x = depthArray[i].X * 0.001f;
                vertex[i].y = -depthArray[i].Y * 0.001f;//上下反転
                vertex[i].z = depthArray[i].Z * 0.001f;

                color[i].b = colorArray[i].B;
                color[i].g = colorArray[i].G;
                color[i].r = colorArray[i].R;
                color[i].a = 255;
            }
            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int index = y * width + x;
                    int a = index;
                    int b = index + 1;
                    int c = index + width;
                    int d = index + width + 1;

                    bool isVaridA = vertex[a].magnitude != 0;
                    bool isVaridB = vertex[b].magnitude != 0;
                    bool isVaridC = vertex[c].magnitude != 0;
                    bool isVaridD = vertex[d].magnitude != 0;

                    if (isVaridA & isVaridB & isVaridC)
                    {
                        indexList.Add(a);
                        indexList.Add(b);
                        indexList.Add(c);
                    }
                    if (isVaridC & isVaridB & isVaridD)
                    {
                        indexList.Add(c);
                        indexList.Add(b);
                        indexList.Add(d);
                    }
                }
            }
        }
    }
}
