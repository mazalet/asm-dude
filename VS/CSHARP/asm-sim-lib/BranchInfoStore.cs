﻿// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AsmSim
{
    public class BranchInfoStore
    {
        #region Fields
        private readonly Context _ctx;
        private IDictionary<string, BranchInfo> _branchInfo;
        #endregion

        #region Constructors
        public BranchInfoStore(Context ctx)
        {
            this._ctx = ctx;
        }
        #endregion

        #region Getters
        public int Count { get { return (this._branchInfo == null) ? 0 : this._branchInfo.Count; } }

        public IEnumerable<BranchInfo> Values
        {
            get
            {
                if (this._branchInfo == null)
                {
                    yield break;
                }
                else
                {
                    foreach (var e in this._branchInfo.Values) yield return e;
                }
            }
        }

        #endregion

        #region Setters
        public void Clear()
        {
            this._branchInfo?.Clear();
        }
        #endregion

        public static BranchInfoStore RetrieveSharedBranchInfo(
            BranchInfoStore store1, BranchInfoStore store2, Context ctx)
        {
            if (store1 == null) return store2;
            if (store2 == null) return store1;

            IList<string> sharedKeys = new List<string>();

            if ((store1._branchInfo != null) && (store2._branchInfo != null))
            {
                ICollection<string> keys1 = store1._branchInfo.Keys;
                ICollection<string> keys2 = store2._branchInfo.Keys;

                foreach (string key in keys1)
                {
                    if (keys2.Contains(key))
                    {
                        if (store1._branchInfo[key].BranchTaken != store2._branchInfo[key].BranchTaken)
                        {
                            sharedKeys.Add(key);
                        }
                        else
                        {
                            //Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: key " + key + " is shared but has equal branchTaken " + state1.BranchInfo[key].branchTaken);
                        }
                    }
                }
                Debug.Assert(sharedKeys.Count <= 1);

                if (false)
                {
                    foreach (string key in keys1)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state1 " + key);
                    }
                    foreach (string key in keys2)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state2 " + key);
                    }
                    foreach (string key in sharedKeys)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: sharedKey " + key);
                    }
                }
            }

            BranchInfoStore mergeBranchStore = new BranchInfoStore(ctx);
            if (sharedKeys.Count == 0)
            {
                if (store1._branchInfo != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store1._branchInfo)
                    {
                        mergeBranchStore.Add(element.Value, true);
                    }
                }
                if (store2._branchInfo != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store2._branchInfo)
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value, true);
                        }
                    }
                }
                //if (!tools.Quiet) Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: the two provided states do not share a branching point. This would happen in loops.");
            }
            else
            {
                //only use the first sharedKey

                string key = sharedKeys[0];
                foreach (KeyValuePair<string, BranchInfo> element in store1._branchInfo)
                {
                    if (!element.Key.Equals(key))
                    {
                        mergeBranchStore.Add(element.Value, true);
                    }
                }
                foreach (KeyValuePair<string, BranchInfo> element in store2._branchInfo)
                {
                    if (!element.Key.Equals(key))
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value, true);
                        }
                    }
                }
            }

            return mergeBranchStore;
        }

        public bool ContainsKey(string key)
        {
            return (this._branchInfo == null) ? false : this._branchInfo.ContainsKey(key);
        }

        public void RemoveKey(string branchInfoKey)
        {
            this._branchInfo.Remove(branchInfoKey);
        }
        public void Remove(BranchInfo branchInfo)
        {
            this._branchInfo.Remove(branchInfo.Key);
        }

        public void Add(BranchInfo branchInfo, bool translate)
        {
            if (branchInfo == null) return;
            if (this._branchInfo == null)
            {
                this._branchInfo = new Dictionary<string, BranchInfo>();
            }

            if (!this._branchInfo.ContainsKey(branchInfo.Key))
            {
                //Console.WriteLine("INFO: AddBranchInfo: key=" + branchInfo.key);
                if (translate)
                {
                    this._branchInfo.Add(branchInfo.Key, branchInfo.Translate(this._ctx));
                }
                else
                {
                    this._branchInfo.Add(branchInfo.Key, branchInfo);
                }
            }
        }
        public void Add(IDictionary<string, BranchInfo> branchInfo, bool translate)
        {
            if (branchInfo == null) return;
            foreach (KeyValuePair<string, BranchInfo> b in branchInfo) this.Add(b.Value, translate);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if ((this._branchInfo != null) && (this._branchInfo.Count > 0))
            {
                sb.AppendLine("Branch control flow constraints:");
                int i = 0;
                foreach (KeyValuePair<string, BranchInfo> entry in this._branchInfo)
                {
                    BoolExpr e = entry.Value.GetData(this._ctx);
                    sb.AppendLine(string.Format("   {0}: {1}", i, ToolsZ3.ToString(e)));
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
