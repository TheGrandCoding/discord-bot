using DiscordBot.Classes.HTMLHelpers;
using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public class TextAmenderBuilder
    {
        public AmendmentBuilder Builder { get; set; }
        public List<TextAmendment> Amends { get; set; }
        public bool MarkDown { get; set; }

        AmendedText Text { get; set; }

        List<AmendedText> texts()
        {
            List<AmendedText> list = new List<AmendedText>();
            var next = Text;
            do
            {
                list.Add(next);
                next = next.Next;
            } while (next != null);
            return list;
        }

        public string NiceWords {  get
            {
                return string.Join(" ", AllWords);
            } }

        public List<string> AllWords { get
            {
                return texts().SelectMany(x => x.Words).ToList();
            } }

        public string RawText { get
            {
                return string.Join("", texts().Select(x => x.FullText));
            } }

        string lhsInsertReplace(string next)
        {
            if (MarkDown)
                return "[";
            return LegHelpers.GetChangeDeliminator(true) + LegHelpers.GetChangeAnchor(next);
        }

        string lhsRemove(string next)
        {
            if (MarkDown)
                return "...";
            return ". . . ." + LegHelpers.GetChangeAnchor(next);
        }

        string rhs()
        {
            if (MarkDown)
                return "]";
            return LegHelpers.GetChangeDeliminator(false);
        }

        public TextAmenderBuilder(string text, AmendmentBuilder builder, List<TextAmendment> amends, bool markDown = false)
        {
            MarkDown = markDown;
            Builder = builder;
            Amends = amends;
            Text = new AmendedText()
            {
                LHS = "",
                RHS = "",
                InnerText = text
            };
            foreach(var amend in amends)
            {
                if (amend.Type == AmendType.Insert)
                    insert(amend);
                else if (amend.Type == AmendType.Repeal)
                    remove(amend);
                else
                    replace(amend);
            }
        }

        void replace(TextAmendment amend)
        {
            AmendedText toAmend = Text;
            while ((toAmend.StartIndex + toAmend.Length) < amend.Start && toAmend != null)
            {
                toAmend = toAmend.Next;
            }
            if (toAmend == null)
                return;
            var next = Builder.GetNextNumber(amend);
            var remover = new AmendedText()
            {
                LHS = lhsInsertReplace(next),
                RHS = rhs(),
                InnerText = amend.New,
            };
            var wordsAfter = toAmend.Words.Skip(amend.Start - toAmend.StartIndex);
            var wordsRemoved = wordsAfter.Take(amend.Length);
            var wordsAfterStr = string.Join(' ', wordsAfter);
            var wordsRemStr = string.Join(' ', wordsRemoved);
            var text = toAmend.InnerText;
            text = text.Substring(0, text.Length - wordsAfterStr.Length);
            var afterText = toAmend.InnerText.Substring(text.Length + wordsRemStr.Length);
            var after = new AmendedText()
            {
                LHS = "",
                RHS = toAmend.RHS,
                InnerText = afterText
            };
            toAmend.RHS = "";
            toAmend.InnerText = text;

            after.Next = toAmend.Next;
            toAmend.Next = remover;
            remover.Next = after;
        }
        void remove(TextAmendment amend)
        {
            AmendedText toAmend = Text;
            while ((toAmend.StartIndex + toAmend.Length) < amend.Start && toAmend != null)
            {
                toAmend = toAmend.Next;
            }
            if (toAmend == null)
                return;
            var next = Builder.GetNextNumber(amend);
            var remover = new AmendedText()
            {
                LHS = lhsRemove(next),
                RHS = "",
                InnerText = ""
            };
            var wordsAfter = toAmend.Words.Skip(amend.Start - toAmend.StartIndex);
            var wordsRemoved = wordsAfter.Take(amend.Length);
            var wordsAfterStr = string.Join(' ', wordsAfter);
            var wordsRemStr = string.Join(' ', wordsRemoved);
            var text = toAmend.InnerText;
            text = text.Substring(0, text.Length - wordsAfterStr.Length);
            var afterText = toAmend.InnerText.Substring(text.Length + wordsRemStr.Length);
            var after = new AmendedText()
            {
                LHS = "",
                RHS = toAmend.RHS,
                InnerText = afterText
            };
            toAmend.RHS = "";
            toAmend.InnerText = text;

            after.Next = toAmend.Next;
            toAmend.Next = remover;
            remover.Next = after;

        }
        void insert(TextAmendment amend)
        {
            AmendedText toAmend = Text;
            while((toAmend.StartIndex + toAmend.Length) < amend.Start && toAmend != null)
            {
                toAmend = toAmend.Next;
            }
            if (toAmend == null)
                return;
            var next = Builder.GetNextNumber(amend);
            var inserter = new AmendedText()
            {
                LHS = lhsInsertReplace(next),
                RHS = rhs(),
                InnerText = amend.New
            };
            var wordsAfter = toAmend.Words.Skip(amend.Start - toAmend.StartIndex);
            var wordsBefore = toAmend.Words.Take(toAmend.Words.Count - wordsAfter.Count());
            toAmend.InnerText = string.Join(' ', wordsBefore);
            var after = new AmendedText()
            {
                RHS = toAmend.RHS,
                InnerText = string.Join(' ', wordsAfter)
            };
            toAmend.RHS = "";

            after.Next = toAmend.Next;
            after.Previous = inserter;

            inserter.Next = after;
            inserter.Previous = toAmend;

            toAmend.Next = inserter;
        }
    }

    class AmendedText
    {
        public string LHS { get; set; }
        public string InnerText { get; set; }
        public string RHS { get; set; }
        public string FullText => $"{LHS}{InnerText}{RHS}";

        AmendedText m_next;
        AmendedText m_previous;
        public AmendedText Previous
        {
            get
            {
                return m_previous;
            } 
            set
            {
                m_previous = value;
                if (value != null)
                    value.m_next = this;
            }
        }
        public AmendedText Next
        {
            get
            {
                return m_next;
            }
            set
            {
                m_next = value;
                if (value != null)
                    value.m_previous = this;
            }
        }

        public int StartIndex {  get
            {
                int c = 0;
                c += (Previous?.StartIndex ?? 0);
                c += (Previous?.Length ?? 0);
                return c;
            } }
        public int Length => Words.Count;
        public List<string> Words => InnerText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
