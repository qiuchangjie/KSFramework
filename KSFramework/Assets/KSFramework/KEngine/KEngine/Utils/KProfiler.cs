﻿#region Copyright (c) 2015 KEngine / Kelly <http://github.com/mr-kelly>, All rights reserved.

// KEngine - Toolset and framework for Unity3D
// ===================================
// 
// Filename: KProfiler.cs
// Date:     2015/12/03
// Author:  Kelly
// Email: 23110388@qq.com
// Github: https://github.com/mr-kelly/KEngine
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Profiling;

namespace KEngine
{
    /// <summary>
    /// 功能：
    ///     1.监视代码运行耗时，内存变化
    /// </summary>
    public class KProfiler
    {
        /// <summary>
        /// 缓存起来的监测器Wachter
        /// </summary>
        private static Dictionary<string, System.Diagnostics.Stopwatch> m_WachterDictionary = null;

        /// <summary>
        /// Watcher内存埋点
        /// </summary>
        private static Dictionary<System.Diagnostics.Stopwatch, long> m_WachterMems = null;

        /// <summary>
        /// 是否可以Watch监测，为了后续方便修改监测条件
        /// 当前设置成DebugBuild才进行监测和输出
        /// </summary>
        public static bool CanWatch
        {
            get { return true; /*UnityEngine.Debug.isDebugBuild;*/ }
        }

        /// <summary>
        /// BeginWatch(string)的任意枚举版
        /// </summary>
        /// <param name="emKey"></param>
        public static void BeginWatch(Enum emKey)
        {
            if (!CanWatch)
                return;
            BeginWatch(emKey.ToString());
        }

        /// <summary>
        /// EndWatch的任意枚举版
        /// </summary>
        /// <param name="emKey"></param>
        public static void EndWatch(Enum emKey)
        {
            if (!CanWatch)
                return;
            EndWatch(emKey.ToString());
        }

        /// <summary>
        /// 使用C#的Stopwatch， debug模式下无行为
        /// </summary>
        /// <param name="key"></param>
        /// <param name="del"></param>
        public static void BeginWatch(string key)
        {
            if (!CanWatch)
                return;

            if (m_WachterDictionary == null)
                m_WachterDictionary = new Dictionary<string, Stopwatch>();
            if (m_WachterMems == null)
                m_WachterMems = new Dictionary<Stopwatch, long>();

            System.Diagnostics.Stopwatch stopwatch;
            if (!m_WachterDictionary.TryGetValue(key, out stopwatch))
            {
                stopwatch = m_WachterDictionary[key] = new System.Diagnostics.Stopwatch();
            }

            m_WachterMems[stopwatch] = GC.GetTotalMemory(false);

            if (stopwatch.IsRunning)
            {
                Log.Error("Running stopwatch need reset: {0}", key);
            }

            stopwatch.Reset();
            stopwatch.Start(); //  开始监视代码运行时间
        }

        /// <summary>
        /// 结束性能监测，输出监测的时间消耗
        /// </summary>
        /// <param name="key"></param>
        public static void EndWatch(string key, string name = null)
        {
            if (!CanWatch)
                return;

            if (m_WachterDictionary == null)
                m_WachterDictionary = new Dictionary<string, Stopwatch>();
            if (m_WachterMems == null)
                m_WachterMems = new Dictionary<Stopwatch, long>();

            System.Diagnostics.Stopwatch stopwatch;
            if (!m_WachterDictionary.TryGetValue(key, out stopwatch))
            {
                Log.Error("Not exist Stopwatch: {0}", key);
                return;
            }
            long lastMem = 0;
            m_WachterMems.TryGetValue(stopwatch, out lastMem);

            stopwatch.Stop(); //  停止监视
            TimeSpan timespan = stopwatch.Elapsed; //  获取当前实例测量得出的总时间
            //double seconds = timespan.TotalSeconds;  //  总秒数
            double millseconds = timespan.TotalMilliseconds;
            decimal seconds = (decimal) millseconds/1000m;

            string format = "[Watcher] {0}, CostTime: {1}s, MemDiff: {2}KB";
            var memDiff = GC.GetTotalMemory(false) - lastMem; // byte
            Log.LogToFile(string.Format(format,
                string.IsNullOrEmpty(name) ? key : name, seconds.ToString("F7"),
                memDiff/1000f)); // 7位精度
        }
        
        /// <summary>
        /// 添加性能观察, 使用C# Stopwatch
        /// </summary>
        /// <param name="callback"></param>
        public static void WatchPerformance(Action callback)
        {
            WatchPerformance("执行耗费时间: {0}s", callback);
        }
        
        /// <summary>
        /// 添加性能观察, 使用C# Stopwatch
        /// </summary>
        public static void WatchPerformance(string outputStr, Action callback)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); //  开始监视代码运行时间

            if (callback != null)
            {
                callback();
            }

            stopwatch.Stop(); //  停止监视
            TimeSpan timespan = stopwatch.Elapsed; //  获取当前实例测量得出的总时间
            //double seconds = timespan.TotalSeconds;  //  总秒数
            double millseconds = timespan.TotalMilliseconds;
            decimal seconds = (decimal)millseconds / 1000m;

            Log.LogToFile(outputStr, seconds.ToString("F7")); // 7位精度
        }

        #region 使用Unity的Time来监视代码耗时
        
        private static float[] RecordTime = new float[10];
        private static string[] RecordKey = new string[10];
        private static int RecordPos = 0;
        /// <summary>
        /// 使用Unity.Time.realtimeSinceStartup监视代码运行时间
        /// </summary>
        /// <param name="key"></param>
        public static void BeginRecordTime(string key)
        {
            if (RecordPos >= RecordTime.Length)
            {
                Log.LogToFile("BeginRecordTime max then 10 count ,will replace first pos data");
                RecordPos = 0;
            }
            RecordTime[RecordPos] = UnityEngine.Time.realtimeSinceStartup;
            RecordKey[RecordPos] = key;
            RecordPos++;
        }

        public static string EndRecordTime(bool printLog = true)
        {
            RecordPos--;
            double s = (UnityEngine.Time.realtimeSinceStartup - RecordTime[RecordPos]);
            if (printLog)
            {
                Log.LogToFile("[RecordTime] {0} use {1}s", RecordKey[RecordPos], s);
            }
            return string.Format("[RecordTime] {0} use {1}s.", RecordKey[RecordPos], s);
        }

        #endregion
        
        /// <summary>
        /// UnityEngine.Profiling.BeginSample
        /// </summary>
        public static void BeginSample(string strName)
        {
            if (!CanWatch) return;
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Profiling.Profiler.BeginSample(strName);
#else
            Profiler.BeginSample(strName);
#endif
        }
        
        /// <summary>
        /// UnityEngine.Profiling.EndSample
        /// </summary>
        public static void EndSample()
        {
            if (!CanWatch) return;
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Profiling.Profiler.EndSample();
#else
            Profiler.EndSample();
#endif
        }
        
        /// <summary>
        /// dump all object
        /// </summary>
        public static void Dump()
        {
            //返回已加载的任何类型的 Unity 对象，包括游戏对象、预制件、材质、网格、纹理等。此函数还将列出内部对象。因此请小心处理返回的对象。 与 Object.FindObjectsOfType 不同的是，此函数还将列出禁用的对象。
            var objs = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
            var objs_group = from obj in objs group obj by obj.GetType();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Dump All Objects:");
            foreach (var kv in objs_group)
            {
                if(kv.Key.IsSubclassOf(typeof(Component)))
                    continue;
                var typeName = kv.Key.Name;
                if(typeName == "MonoScript" || typeName =="Object" || typeName == "GameObject")
                    continue;
                builder.AppendLine(string.Format("type:{0} ,objs:",kv.Key));
                foreach (var  obj in kv)
                {
                    #region 存在于场景中的

                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                        continue;
                    #if UNITY_EDITOR
                    if(obj is GameObject)
                    {
                        var go =  obj as GameObject;
                        //is stored in disk
                        if (go.transform && !EditorUtility.IsPersistent(go.transform.root.gameObject))
                            continue;
                        if (string.IsNullOrEmpty(go.scene.name))
                            continue;

                    }
                    #endif
                    #endregion
                    
                    var mem = Profiler.GetRuntimeMemorySizeLong(obj) * 0.001;
                    var tex = obj as Texture;
                    if (tex != null)
                    {
                        if (obj is Texture2D)
                        {
                            var t2d = obj as Texture2D;
                            builder.AppendLine(string.Format("\t\t name:{0} ,mem:{1}MB ,wh:{2}x{3}, mipmapCount:{4} ,format:{5}",obj,mem,t2d.width,t2d.height,t2d.mipmapCount,t2d.format));
                        }
                        else if (obj is RenderTexture)
                        {
                            var rt = obj as RenderTexture;
                            builder.AppendLine(string.Format("\t\t name:{0} ,mem:{1}MB ,wh:{2}x{3}, genMinmap:{4} ,format:{5} ,depth:{6}",obj,mem,rt.width,rt.height,rt.autoGenerateMips,rt.format,rt.depth));
                        }
                        else
                        {
                            builder.AppendLine(string.Format("\t\t name:{0} ,mem:{1}MB ,wh:{2}x{3}, anisoLevel:{4}",obj,mem,tex.width,tex.height,tex.anisoLevel));    
                        }
                    }
                    else
                    {
                        builder.AppendLine(string.Format("\t\t name:{0} ,mem:{1}MB",obj,mem));    
                    }
                    
                }
                builder.AppendLine("\r\n");
            }
            if(Application.isEditor) 
                UnityEngine.Debug.Log(builder.ToString());
            else
                Log.LogToFile(builder.ToString());
        }
    }
}