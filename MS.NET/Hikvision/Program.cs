﻿using System;
using System.IO;
using Jaya.Hikvision.Sdk;
using Jaya.Hikvision.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Jaya.Hikvision
{
    public class Program
    {
        private static int USER_ID = -1;
        private static uint A_CHAN_NUM = 0;
        private static uint D_CHAN_NUM = 0;
        private static uint LAST_ERROR_CODE = 0;
        private static List<int> CHANNEL_LIST = new List<int>();
        private static List<int> IP_DEVICE_ID = new List<int>();
        private static CHCNetSDK.NET_DVR_IPCHANINFO IP_CHAN_INFO;
        private static CHCNetSDK.NET_DVR_PU_STREAM_URL STREAM_URL;
        private static CHCNetSDK.NET_DVR_DEVICEINFO_V30 DEVICE_INFO;
        private static CHCNetSDK.NET_DVR_IPPARACFG_V40 IP_PARAM_CFG_V40;
        private static CHCNetSDK.NET_DVR_IPCHANINFO_V40 IP_CHAN_INFO_V40;
        public static void Main(string[] args)
        {
            var NVR_CONFIG = new { Ip = "10.0.5.235", Port = 8000, Channel = 33, Uid = "admin", Pwd = "admin12345" };

            try
            {
                #region 初始化SDK

                Log.WriteLog("初始化SDK开始");
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：初始化SDK开始");
                var result = CHCNetSDK.NET_DVR_Init();
                Log.WriteLog("初始化SDK结束");
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：初始化SDK结束");
                if (result)
                {
                    Log.WriteLog("初始化SDK成功");
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：初始化SDK成功");

                    CHCNetSDK.NET_DVR_SetLogToFile(3, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"), true);

                    Log.WriteLog($"用户注册设备【{NVR_CONFIG.Ip}】开始");
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注册设备【{NVR_CONFIG.Ip}】开始");
                    USER_ID = CHCNetSDK.NET_DVR_Login_V30(NVR_CONFIG.Ip, NVR_CONFIG.Port, NVR_CONFIG.Uid, NVR_CONFIG.Pwd, ref DEVICE_INFO);
                    Log.WriteLog($"用户注册设备【{NVR_CONFIG.Ip}】结束");
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注册设备【{NVR_CONFIG.Ip}】结束");
                    if (USER_ID < 0)
                    {
                        LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                        Log.WriteLog($"用户注册设备【{NVR_CONFIG.Ip}】失败，错误码：{LAST_ERROR_CODE}");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注册设备【{NVR_CONFIG.Ip}】失败，错误码：{LAST_ERROR_CODE}");
                    }
                    else
                    {
                        Log.WriteLog($"用户注册设备【{NVR_CONFIG.Ip}】成功");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注册设备【{NVR_CONFIG.Ip}】成功");

                        A_CHAN_NUM = DEVICE_INFO.byChanNum;
                        D_CHAN_NUM = DEVICE_INFO.byIPChanNum + 256 * (uint)DEVICE_INFO.byHighDChanNum;
                        if (D_CHAN_NUM <= 0)
                        {
                            for (int i = 0; i < A_CHAN_NUM; i++)
                            {
                                CHANNEL_LIST.Add(i + DEVICE_INFO.byStartChan);
                            }
                        }
                        else
                        {
                            var dwSize = (uint)Marshal.SizeOf(IP_PARAM_CFG_V40);

                            var ptrIpParaCfgV40 = Marshal.AllocHGlobal((int)dwSize);
                            Marshal.StructureToPtr(IP_PARAM_CFG_V40, ptrIpParaCfgV40, false);

                            var iGroupNo = 0;
                            var dwReturn = 0U;
                            if (CHCNetSDK.NET_DVR_GetDVRConfig(USER_ID, CHCNetSDK.NET_DVR_GET_IPPARACFG_V40, iGroupNo, ptrIpParaCfgV40, dwSize, ref dwReturn))
                            {
                                IP_PARAM_CFG_V40 = (CHCNetSDK.NET_DVR_IPPARACFG_V40)Marshal.PtrToStructure(ptrIpParaCfgV40, typeof(CHCNetSDK.NET_DVR_IPPARACFG_V40));

                                for (int i = 0; i < A_CHAN_NUM; i++)
                                {
                                    if (IP_PARAM_CFG_V40.byAnalogChanEnable[i] == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        CHANNEL_LIST.Add(i + DEVICE_INFO.byStartChan);
                                    }
                                }

                                byte iStreamType = 0;
                                var iDChanNum = D_CHAN_NUM < 64 ? D_CHAN_NUM : 64U; /// 如果设备IP通道小于64路，按实际路数获取

                                for (int i = 0; i < iDChanNum; i++)
                                {
                                    iStreamType = IP_PARAM_CFG_V40.struStreamMode[i].byGetStreamType;

                                    dwSize = (uint)Marshal.SizeOf(IP_PARAM_CFG_V40.struStreamMode[i].uGetStream);
                                    switch (iStreamType)
                                    {
                                        /// 目前NVR仅支持直接从设备取流 NVR supports only the mode: get stream from device directly
                                        case 0:
                                            {
                                                var ptrChanInfo = Marshal.AllocHGlobal((int)dwSize);
                                                Marshal.StructureToPtr(IP_PARAM_CFG_V40.struStreamMode[i].uGetStream, ptrChanInfo, false);
                                                IP_CHAN_INFO = (CHCNetSDK.NET_DVR_IPCHANINFO)Marshal.PtrToStructure(ptrChanInfo, typeof(CHCNetSDK.NET_DVR_IPCHANINFO));

                                                if (IP_CHAN_INFO.byIPID == 0 || IP_CHAN_INFO.byEnable == 0)
                                                {
                                                    IP_DEVICE_ID.Add(IP_CHAN_INFO.byIPID + IP_CHAN_INFO.byIPIDHigh * 256 - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrChanInfo);
                                                }
                                                else
                                                {
                                                    CHANNEL_LIST.Add(i + (int)IP_PARAM_CFG_V40.dwStartDChan);

                                                    IP_DEVICE_ID.Add(IP_CHAN_INFO.byIPID + IP_CHAN_INFO.byIPIDHigh * 256 - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrChanInfo);
                                                }

                                                break;
                                            }
                                        case 4:
                                            {
                                                var ptrStreamURL = Marshal.AllocHGlobal((int)dwSize);
                                                Marshal.StructureToPtr(IP_PARAM_CFG_V40.struStreamMode[i].uGetStream, ptrStreamURL, false);
                                                STREAM_URL = (CHCNetSDK.NET_DVR_PU_STREAM_URL)Marshal.PtrToStructure(ptrStreamURL, typeof(CHCNetSDK.NET_DVR_PU_STREAM_URL));

                                                if (STREAM_URL.wIPID == 0 || STREAM_URL.byEnable == 0)
                                                {
                                                    IP_DEVICE_ID.Add(STREAM_URL.wIPID - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrStreamURL);
                                                }
                                                else
                                                {
                                                    CHANNEL_LIST.Add(i + (int)IP_PARAM_CFG_V40.dwStartDChan);

                                                    IP_DEVICE_ID.Add(STREAM_URL.wIPID - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrStreamURL);
                                                }

                                                break;
                                            }
                                        case 6:
                                            {
                                                var ptrChanInfoV40 = Marshal.AllocHGlobal((int)dwSize);
                                                Marshal.StructureToPtr(IP_PARAM_CFG_V40.struStreamMode[i].uGetStream, ptrChanInfoV40, false);
                                                IP_CHAN_INFO_V40 = (CHCNetSDK.NET_DVR_IPCHANINFO_V40)Marshal.PtrToStructure(ptrChanInfoV40, typeof(CHCNetSDK.NET_DVR_IPCHANINFO_V40));

                                                if (IP_CHAN_INFO_V40.wIPID == 0 || IP_CHAN_INFO_V40.byEnable == 0)
                                                {
                                                    IP_DEVICE_ID.Add(IP_CHAN_INFO_V40.wIPID - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrChanInfoV40);
                                                }
                                                else
                                                {
                                                    CHANNEL_LIST.Add(i + (int)IP_PARAM_CFG_V40.dwStartDChan);

                                                    IP_DEVICE_ID.Add(IP_CHAN_INFO_V40.wIPID - iGroupNo * 64 - 1);

                                                    Marshal.FreeHGlobal(ptrChanInfoV40);
                                                }

                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                                Log.WriteLog($"获取设备【{NVR_CONFIG.Ip}】（NET_DVR_GET_IPPARACFG_V40）失败，错误码：{LAST_ERROR_CODE}");
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：获取设备【{NVR_CONFIG.Ip}】（NET_DVR_GET_IPPARACFG_V40）失败，错误码：{LAST_ERROR_CODE}");
                            }

                            Marshal.FreeHGlobal(ptrIpParaCfgV40);
                        }

                        //CHANNEL_LIST.ForEach(iChannel =>
                        //{
                        //    var dtStart = DateTime.Now;
                        //    var lpJpegParams = new CHCNetSDK.NET_DVR_JPEGPARA();
                        //    lpJpegParams.wPicQuality = 0;   /// 图像质量 Image quality
                        //    lpJpegParams.wPicSize = 0xFF;   /// 抓图分辨率 Picture size: 0xff-Auto（使用当前码流分辨率）

                        //    var strJpegFileUrl = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", $"{DateTime.Now:yyyyMMddHHmmssfff}-{iChannel}.jpeg");
                        //    Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）开始，保存文件：{strJpegFileUrl}");
                        //    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）开始，保存文件：{strJpegFileUrl}");
                        //    result = CHCNetSDK.NET_DVR_CaptureJPEGPicture(USER_ID, iChannel, ref lpJpegParams, strJpegFileUrl);
                        //    Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）结束，保存文件：{strJpegFileUrl}");
                        //    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）结束，保存文件：{strJpegFileUrl}");
                        //    if (result)
                        //    {
                        //        Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）成功，保存文件：{strJpegFileUrl}");
                        //        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）成功，保存文件：{strJpegFileUrl}");
                        //    }
                        //    else
                        //    {
                        //        LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                        //        Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）失败，保存文件：{strJpegFileUrl}，错误码：{LAST_ERROR_CODE}");
                        //        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）失败，保存文件：{strJpegFileUrl}，错误码：{LAST_ERROR_CODE}");
                        //    }
                        //    var dtEnd = DateTime.Now;

                        //    var timespan = dtEnd - dtStart;

                        //    Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）耗时：{timespan}");
                        //    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）耗时：{timespan}");
                        //});

                        Parallel.ForEach(CHANNEL_LIST, iChannel =>
                        {
                            var dtStart = DateTime.Now;
                            var lpJpegParams = new CHCNetSDK.NET_DVR_JPEGPARA();
                            lpJpegParams.wPicQuality = 0;   /// 图像质量 Image quality
                            lpJpegParams.wPicSize = 0xFF;   /// 抓图分辨率 Picture size: 0xff-Auto（使用当前码流分辨率）
                            while (true)
                            {
                                var strJpegFileUrl = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", $"{DateTime.Now:yyyyMMddHHmmssfff}-{iChannel}.jpeg");
                                Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）开始，保存文件：{strJpegFileUrl}");
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）开始，保存文件：{strJpegFileUrl}");
                                result = CHCNetSDK.NET_DVR_CaptureJPEGPicture(USER_ID, iChannel, ref lpJpegParams, strJpegFileUrl);
                                Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）结束，保存文件：{strJpegFileUrl}");
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）结束，保存文件：{strJpegFileUrl}");
                                if (result)
                                {
                                    Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）成功，保存文件：{strJpegFileUrl}");
                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）成功，保存文件：{strJpegFileUrl}");
                                }
                                else
                                {
                                    LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                                    Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）失败，保存文件：{strJpegFileUrl}，错误码：{LAST_ERROR_CODE}");
                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）失败，保存文件：{strJpegFileUrl}，错误码：{LAST_ERROR_CODE}");
                                }
                                var dtEnd = DateTime.Now;

                                var timespan = dtEnd - dtStart;

                                Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）耗时：{timespan}，保存文件：{strJpegFileUrl}");
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_CaptureJPEGPicture）耗时：{timespan}，保存文件：{strJpegFileUrl}");
                            }
                        });

                        Log.WriteLog($"用户注销设备【{NVR_CONFIG.Ip}】开始");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注销设备【{NVR_CONFIG.Ip}】开始");
                        result = CHCNetSDK.NET_DVR_Logout_V30(USER_ID);
                        Log.WriteLog($"用户注销设备【{NVR_CONFIG.Ip}】结束");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注销设备【{NVR_CONFIG.Ip}】结束");
                        if (result)
                        {
                            Log.WriteLog($"用户注销设备【{NVR_CONFIG.Ip}】成功");
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注销设备【{NVR_CONFIG.Ip}】成功");
                        }
                        else
                        {
                            LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                            Log.WriteLog($"用户注销设备【{NVR_CONFIG.Ip}】失败，错误码：{LAST_ERROR_CODE}");
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：用户注销设备【{NVR_CONFIG.Ip}】失败，错误码：{LAST_ERROR_CODE}");
                        }

                        Log.WriteLog($"释放设备【{NVR_CONFIG.Ip}】SDK资源开始");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：释放设备【{NVR_CONFIG.Ip}】SDK资源开始");
                        result = CHCNetSDK.NET_DVR_Cleanup();
                        Log.WriteLog($"释放设备【{NVR_CONFIG.Ip}】SDK资源结束");
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：释放设备【{NVR_CONFIG.Ip}】SDK资源结束");
                        if (result)
                        {
                            Log.WriteLog($"释放设备【{NVR_CONFIG.Ip}】SDK资源成功");
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：释放设备【{NVR_CONFIG.Ip}】SDK资源成功");
                        }
                        else
                        {
                            LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                            Log.WriteLog($"释放设备【{NVR_CONFIG.Ip}】SDK资源失败，错误码：{LAST_ERROR_CODE}");
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：释放设备【{NVR_CONFIG.Ip}】SDK资源失败，错误码：{LAST_ERROR_CODE}");
                        }
                    }
                }
                else
                {
                    LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                    Log.WriteLog($"初始化SDK失败，错误码：{LAST_ERROR_CODE}");
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：初始化SDK失败，错误码：{LAST_ERROR_CODE}");
                }

                #endregion
            }
            catch (Exception e)
            {
                Log.WriteLog(e);
                Console.WriteLine(e.Message);
                LAST_ERROR_CODE = CHCNetSDK.NET_DVR_GetLastError();
                Log.WriteLog($"设备【{NVR_CONFIG.Ip}】（NET_DVR_Init）失败，错误码：{LAST_ERROR_CODE}");
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fffffff}：设备【{NVR_CONFIG.Ip}】（NET_DVR_Init）失败，错误码：{LAST_ERROR_CODE}");
            }

            Console.ReadKey();
        }
    }
}