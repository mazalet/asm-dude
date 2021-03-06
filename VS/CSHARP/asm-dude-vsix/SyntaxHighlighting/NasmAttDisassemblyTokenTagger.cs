﻿// The MIT License (MIT)
//
// Copyright (c) 2018 Henk-Jan Lebbink
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

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmTools;
using System.Globalization;

namespace AsmDude
{
    internal sealed class NasmAttDisassemblyTokenTagger : ITagger<AsmTokenTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly AsmDudeTools _asmDudeTools = null;

        private readonly AsmTokenTag _mnemonic;
        private readonly AsmTokenTag _register;
        private readonly AsmTokenTag _remark;
        private readonly AsmTokenTag _directive;
        private readonly AsmTokenTag _constant;
        private readonly AsmTokenTag _jump;
        private readonly AsmTokenTag _label;
        private readonly AsmTokenTag _labelDef;
        private readonly AsmTokenTag _misc;
        //private readonly AsmTokenTag _userDefined1;
        //private readonly AsmTokenTag _userDefined2;
        //private readonly AsmTokenTag _userDefined3;
        //private readonly AsmTokenTag _UNKNOWN;

        internal NasmAttDisassemblyTokenTagger(ITextBuffer buffer)
        {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeTools.Instance;

            this._mnemonic = new AsmTokenTag(AsmTokenType.Mnemonic);
            this._register = new AsmTokenTag(AsmTokenType.Register);
            this._remark = new AsmTokenTag(AsmTokenType.Remark);
            this._directive = new AsmTokenTag(AsmTokenType.Directive);
            this._constant = new AsmTokenTag(AsmTokenType.Constant);
            this._jump = new AsmTokenTag(AsmTokenType.Jump);
            this._label = new AsmTokenTag(AsmTokenType.Label);
            this._labelDef = new AsmTokenTag(AsmTokenType.LabelDef);
            this._misc = new AsmTokenTag(AsmTokenType.Misc);
            //this._userDefined1 = new AsmTokenTag(AsmTokenType.UserDefined1);
            //this._userDefined2 = new AsmTokenTag(AsmTokenType.UserDefined2);
            //this._userDefined3 = new AsmTokenTag(AsmTokenType.UserDefined3);
            //this._UNKNOWN = new AsmTokenTag(AsmTokenType.UNKNOWN);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime time1 = DateTime.Now;

            if (spans.Count == 0)
            {  //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string line = containingLine.GetText().ToUpper();
                var pos = new List<(int BeginPos, int Length, bool IsLabel)>(AsmSourceTools.SplitIntoKeywordPos(line));

                int offset = containingLine.Start.Position;
                int nKeywords = pos.Count;

	            // if the line does not contain a Mnemonic, assume it is a source code line and make it a remark
                #region Check source code line
                if (IsSourceCode(line, pos))
                {
                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span((0, line.Length, false), offset, curSpan), this._remark);
                    continue; // go to the next line
                }
                #endregion

                for (int k = 0; k < nKeywords; k++)
                {
                    string asmToken = NasmIntelTokenTagger.Keyword(pos[k], line);

                    // keyword k is a label definition
                    if (pos[k].IsLabel)
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._labelDef);
                        continue;
                    }
	   
                    AsmTokenType keywordType = this._asmDudeTools.Get_Token_Type_Att(asmToken);
                    switch (keywordType)
                    {
                        case AsmTokenType.Jump:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._jump);

                                k++; // goto the next word
                                if (k == nKeywords) break; // there are no next words

                                string asmToken2 = NasmIntelTokenTagger.Keyword(pos[k], line);
                                switch (asmToken2)
                                {
                                    case "WORD":
                                    case "DWORD":
                                    case "QWORD":
                                    case "SHORT":
                                    case "NEAR":
                                        {
                                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._misc);

                                            k++;
                                            if (k == nKeywords) break;
                                            string asmToken3 = NasmIntelTokenTagger.Keyword(pos[k], line);
                                            switch (asmToken3)
                                            {
                                                case "PTR":
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(New_Span(pos[k], offset, curSpan), this._misc);
                                                        break;
                                                    }
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._label);
                                                    break;
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            if (RegisterTools.IsRegister(asmToken2))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._register);
                                            }
                                            else
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._label);
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            {
                                //if (AsmTools.AsmSourceTools.Parse_Constant(asmToken, true).Valid)
                                if (AsmSourceTools.Evaluate_Constant(asmToken, true).Valid)
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                }
                                else if (asmToken.StartsWith("\"") && asmToken.EndsWith("\""))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                }
                                else
                                {
                                     //yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._UNKNOWN);
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._directive);
                                break;
                            }
                        case AsmTokenType.Mnemonic:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._mnemonic);
                                break;
                            }
                        case AsmTokenType.Register:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._register);
                                break;
                            }
                        default: break;
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "NasmAttDisassemblyTokenTagger");
        }

        #region Private Member Methods

        private static bool IsConstant(string token)
        {
            if (long.TryParse(token, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var dummy1))
            {
                return true;
            }
            //if (long.TryParse(token, NumberStyles.Integer, CultureInfo.CurrentCulture, out var dummy2))
            //{
            //    return true;
            //}
            if (token.EndsWith("H"))
            {
                return true;
            }
            return false;
        }

        private static bool IsSourceCode(string line, List<(int BeginPos, int Length, bool IsLabel)> pos)
        {
            if (pos.Count == 0) return true;

            // just some rules of thumb
            if (line.Contains(";")) return true;
            {
                string line2 = line.Trim();
                if (line2.StartsWith("---")) return true;
                if (line2.StartsWith("{")) return true;
                if (line2.StartsWith("}")) return true;
                if (line2.StartsWith("/")) return true;
                if (line2.Contains("__CDECL")) return true;
            }

            if (pos[0].IsLabel) return false;

            foreach (var v in pos)
            {
                string asmToken = NasmIntelTokenTagger.Keyword(v, line);
                if (AsmSourceTools.ParseMnemonic(asmToken, true) != Mnemonic.NONE)
                {
                    return false; // found an assebly instruction, think this is assembly code
                }
            }
            return true;
        }

        #endregion

        #region Public Static Methods

        public static string Keyword((int, int, bool) pos, string line)
        {
            return line.Substring(pos.Item1, pos.Item2 - pos.Item1);
        }

        public static SnapshotSpan New_Span((int, int, bool) pos, int offset, SnapshotSpan lineSnapShot)
        {
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.Item1 + offset, pos.Item2 - pos.Item1));
        }

        #endregion Public Static Methods
    }
}
