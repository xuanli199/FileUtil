using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileOccupyDetector
{
    public static class FileUtil
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int RmRebootReasonNone = 0;
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
                                            uint nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications,
                                            uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
                                    ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
                                    ref uint lpdwRebootReasons);

        /// <summary>
        /// 查找占用指定文件或文件夹的所有进程
        /// </summary>
        /// <param name="path">文件或文件夹路径</param>
        /// <returns>占用该文件或文件夹的进程列表</returns>
        public static List<Process> GetProcessesLockingFile(string path)
        {
            List<Process> processes = new List<Process>();

            // 检查路径是否存在
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return processes;
            }

            // 如果是目录，递归检查所有文件
            if (Directory.Exists(path))
            {
                try
                {
                    // 获取目录中的所有文件
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var fileProcesses = GetProcessesLockingFile(file);
                        foreach (var process in fileProcesses)
                        {
                            if (!processes.Exists(p => p.Id == process.Id))
                            {
                                processes.Add(process);
                            }
                        }
                    }
                    return processes;
                }
                catch (Exception)
                {
                    // 如果无法访问某些文件或子目录，继续处理其他可访问的文件
                    return processes;
                }
            }

            uint handle;
            string key = Guid.NewGuid().ToString();
            int result = RmStartSession(out handle, 0, key);

            if (result != 0)
                throw new Exception("无法启动重启管理器会话");

            try
            {
                const int ERROR_MORE_DATA = 234;
                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = RmRebootReasonNone;

                string[] resources = new string[] { path };
                result = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                if (result != 0)
                    throw new Exception("无法注册资源");

                // 获取需要的进程信息数量
                result = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (result == ERROR_MORE_DATA)
                {
                    // 分配足够的内存并获取进程信息
                    pnProcInfo = pnProcInfoNeeded;
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfo];
                    result = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

                    if (result == 0)
                    {
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                            }
                            catch (ArgumentException)
                            {
                                // 进程可能已经结束，忽略
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("无法获取进程列表");
                    }
                }
                else if (result != 0)
                {
                    throw new Exception("获取进程列表时出错");
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        /// <summary>
        /// 尝试释放文件占用
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功释放</returns>
        public static bool TryReleaseFile(string filePath)
        {
            try
            {
                // 尝试打开文件进行写入，如果成功则表示文件未被占用
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}