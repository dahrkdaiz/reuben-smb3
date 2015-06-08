﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FastColoredTextBoxNS;
using Reuben.Controllers;

namespace Reuben.UI
{
    public class ASMFastColoredTextBox : FastColoredTextBox
    {
        Style ASMCommandStyle = new TextStyle(Brushes.Blue, null, FontStyle.Bold);
        Style ASMRegisterStyle = new TextStyle(Brushes.Blue, null, FontStyle.Bold);
        Style ASMCommentStyle = new TextStyle(Brushes.Gray, null, FontStyle.Italic);
        Style ASMDirectiveStyle = new TextStyle(Brushes.Purple, null, FontStyle.Regular);
        Style ASMNumericalStyle = new TextStyle(Brushes.Green, null, FontStyle.Regular);
        Style ASMTagStyle = new TextStyle(Brushes.Maroon, null, FontStyle.Bold);

        private string ASMCommentRegEx;
        private string ASMCommandRegEx;
        private string ASMDirectiveRegEx;
        private string ASMNumericalRegEx;
        private string ASMRegisterRegEx;
        private string ASMMemoryRegEx;
        private string ASMTagRegEx;


        private ASMController localASMController;
        public string File { get; private set;  }
        public void Initiliaze(ASMController asmController, string file)
        {
            localASMController = asmController;
            AutoIndent = false;

            ASMCommentRegEx = ";.*$";
            ASMDirectiveRegEx = "\\.[A-Za-z]+";
            ASMCommandRegEx = "^\\s{4}[A-Z]{3}\\s";
            ASMNumericalRegEx = "\\s\\#[\\-$0-9A-Fa-f]+|\\s[0-9\\-]+|\\%[01]{8}";
            ASMMemoryRegEx = "\\$[0-9A-F]{2}|\\$[0-9A-F]{3}|\\$[0-9A-F]{4}|\\-\\$[0-9A-F]{3}";
            ASMRegisterRegEx = "(?<=,)[AXYaxy]|(?<=, )[AXYaxy]";
            ASMTagRegEx = "\\;\\#[A-Za-z0-9\\-_.@]+";

            this.Text = asmController.GetFile(file);

            AutocompleteMenu popupMenu = new AutocompleteMenu(this);
            popupMenu.MinFragmentLength = 3;
            popupMenu.Items.SetAutocompleteItems(asmController.ParseSymbols(this.Text));
            popupMenu.Items.MaximumSize = new System.Drawing.Size(200, 300);
            popupMenu.Items.Width = 200;
            popupMenu.AllowTabKey = true;


            File = file;
            this.TextChangedDelayed += ASMFastColoredTextBox_TextChangedDelayed;
        }


        public void Save()
        {
            localASMController.Save(File, this.Text);
        }



        public void UpdateLine(int line, string text)
        {
            text = text.Replace("\t", "    ");
            TextSource[line].Clear();
            TextSource[line].AddRange(text.Select(c => new FastColoredTextBoxNS.Char(c)));

            Range Range = new Range(this, 0, line, text.Length, line);

            Range.ClearStyle(ASMCommandStyle, ASMCommentStyle, ASMDirectiveStyle, ASMNumericalStyle, ASMRegisterStyle, ASMTagStyle);
            Range.SetStyle(ASMTagStyle, ASMTagRegEx);
            Range.SetStyle(ASMCommentStyle, ASMCommentRegEx, RegexOptions.Multiline);
            Range.SetStyle(ASMCommandStyle, ASMCommandRegEx, RegexOptions.Multiline);
            Range.SetStyle(ASMDirectiveStyle, ASMDirectiveRegEx);
            Range.SetStyle(ASMNumericalStyle, ASMNumericalRegEx);
            Range.SetStyle(ASMNumericalStyle, ASMMemoryRegEx);
            Range.SetStyle(ASMRegisterStyle, ASMRegisterRegEx);
            Invalidate();
        }
        void ASMFastColoredTextBox_TextChangedDelayed(object sender, TextChangedEventArgs e)
        {
            Range.ClearStyle(ASMCommandStyle, ASMCommentStyle, ASMDirectiveStyle, ASMNumericalStyle, ASMRegisterStyle, ASMTagStyle);
            Range.SetStyle(ASMTagStyle, ASMTagRegEx);
            Range.SetStyle(ASMCommentStyle, ASMCommentRegEx, RegexOptions.Multiline);
            Range.SetStyle(ASMCommandStyle, ASMCommandRegEx, RegexOptions.Multiline);
            Range.SetStyle(ASMDirectiveStyle, ASMDirectiveRegEx);
            Range.SetStyle(ASMNumericalStyle, ASMNumericalRegEx);
            Range.SetStyle(ASMNumericalStyle, ASMMemoryRegEx);
            Range.SetStyle(ASMRegisterStyle, ASMRegisterRegEx);
        }


        public string GoToTag(string text)
        {
            if (text.Contains("@"))
            {

                // find offset - #ObjectsInit@39
                //  ;#ObjectsInit.word@28

                //      .word DSKFWEERD
                this.Selection = new FastColoredTextBoxNS.Range(this, new Place(0, 0), new Place(1, 0));
                string[] split1 = text.Split('@'); // "#ObjectsInit, 39
                int myOffset = Convert.ToInt32(split1[1].Substring(0, 2), 16); // 0x39

                string tagLine = FindAndGetLine(split1[0], 0); // ;#ObjectsInit.word@28
                string[] split2 = tagLine.Split('.', '@'); // ;#ObjectsInit, word, 28

                int startOffset = Convert.ToInt32(split2[2].Substring(0, 2), 16); // 0x28
                int actualOffset = (myOffset - startOffset) + 1; // 0x11

                string foundLine = FindAndGetLine(split2[1], actualOffset);
                string[] words = foundLine.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                string indirection = null;

                split2[1] = "." + split2[1];
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i] == split2[1])
                    {
                        indirection = words[i + 1];
                        break;
                    }
                }

                return InternalFindNext(indirection + ":") != null ? null : indirection;
            }
            else
            {
                return InternalFindNext(text) != null ? null : text;
            }
          
        }

        private Range InternalFindNext(string pattern)
        {
            Place startPlace = new Place(0, 0);
            pattern = Regex.Escape(pattern);
            Range range = this.Selection.Clone();
            range.Normalize();

            range.Start = range.End;
            if (range.Start >= startPlace)
            {
                range.End = new Place(GetLineLength(LinesCount - 1), LinesCount - 1);
            }
            else
            {
                range.End = startPlace;
            }
            foreach (var r in range.GetRangesByLines(pattern, RegexOptions.None))
            {
                this.Selection = r;
                this.DoSelectionVisible();
                this.Invalidate();
                return r;
            }

            if (range.Start >= startPlace && startPlace > Place.Empty)
            {
                this.Selection.Start = new Place(0, 0);
                return InternalFindNext(pattern);
            }

            return null;
        }

        private string FindAndGetLine(string pattern, int offset)
        {
            Range foundLine = null;
            while (offset >= 0)
            {
                foundLine = InternalFindNext(pattern);
                offset--;
            }

            if (foundLine != null)
            {
                return GetLine(foundLine.End.iLine).Text;
            }

            return null;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ASMFastColoredTextBox
            // 
            this.AutoScrollMinSize = new System.Drawing.Size(25, 15);
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Name = "ASMFastColoredTextBox";
            this.ResumeLayout(false);

        }

        private void ASMFastColoredTextBox_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
