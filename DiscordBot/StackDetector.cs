using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DiscordBot
{
    // A simple decompiler that extracts all method tokens (that is: call, callvirt, newobj in IL)
    internal class Decompiler
    {
        private Decompiler() { }

        static Decompiler()
        {
            singleByteOpcodes = new OpCode[0x100];
            multiByteOpcodes = new OpCode[0x100];
            FieldInfo[] infoArray1 = typeof(OpCodes).GetFields();
            for (int num1 = 0; num1 < infoArray1.Length; num1++)
            {
                FieldInfo info1 = infoArray1[num1];
                if (info1.FieldType == typeof(OpCode))
                {
                    OpCode code1 = (OpCode)info1.GetValue(null);
                    ushort num2 = (ushort)code1.Value;
                    if (num2 < 0x100)
                    {
                        singleByteOpcodes[(int)num2] = code1;
                    }
                    else
                    {
                        if ((num2 & 0xff00) != 0xfe00)
                        {
                            throw new Exception("Invalid opcode: " + num2.ToString());
                        }
                        multiByteOpcodes[num2 & 0xff] = code1;
                    }
                }
            }
        }

        private static OpCode[] singleByteOpcodes;
        private static OpCode[] multiByteOpcodes;

        public static MethodBase[] Decompile(MethodBase mi, byte[] ildata)
        {
            HashSet<MethodBase> result = new HashSet<MethodBase>();

            Module module = mi.Module;

            int position = 0;
            while (position < ildata.Length)
            {
                OpCode code = OpCodes.Nop;

                ushort b = ildata[position++];
                if (b != 0xfe)
                {
                    code = singleByteOpcodes[b];
                }
                else
                {
                    b = ildata[position++];
                    code = multiByteOpcodes[b];
                    b |= (ushort)(0xfe00);
                }

                switch (code.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineField:
                    case OperandType.InlineI:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.ShortInlineR:
                        position += 4;
                        break;
                    case OperandType.InlineR:
                    case OperandType.InlineI8:
                        position += 8;
                        break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(ildata, position);
                        position += count * 4 + 4;
                        break;

                    case OperandType.InlineMethod:
                        int methodId = BitConverter.ToInt32(ildata, position);
                        position += 4;
                        try
                        {
                            if (mi is ConstructorInfo)
                            {
                                result.Add((MethodBase)module.ResolveMember(methodId, mi.DeclaringType.GetGenericArguments(), Type.EmptyTypes));
                            }
                            else
                            {
                                result.Add((MethodBase)module.ResolveMember(methodId, mi.DeclaringType.GetGenericArguments(), mi.GetGenericArguments()));
                            }
                        }
                        catch { }
                        break;


                    default:
                        throw new Exception("Unknown instruction operand; cannot continue. Operand type: " + code.OperandType);
                }
            }
            return result.ToArray();
        }
    }

    class StackOverflowDetector
    {
        public static void RecursionDetector()
        {
            // First decompile all methods in the assembly:
            Dictionary<MethodBase, MethodBase[]> calling = new Dictionary<MethodBase, MethodBase[]>();
            var assembly = typeof(StackOverflowDetector).Assembly;

            foreach (var type in assembly.GetTypes())
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OfType<MethodBase>())
                {
                    var body = member.GetMethodBody();
                    if (body != null)
                    {
                        var bytes = body.GetILAsByteArray();
                        if (bytes != null)
                        {
                            // Store all the calls of this method:
                            var calls = Decompiler.Decompile(member, bytes);
                            calling[member] = calls;
                        }
                    }
                }
            }

            var ignored = new string[]
            {
                "DiscordBot.Program.Close",
                "DiscordBot.Program.AttemptParseInput",
                "DiscordBot.Program.Save",
                "DiscordBot.JsonReaderCreater.RegisterObj",
                "DiscordBot.JsonReaderCreater.get_Prefix",
                "DiscordBot.JsonReaderCreater.getToken",
                "DiscordBot.JsonReaderCreater.GetModelString",
                "DiscordBot.JsonReaderCreater..ctor",
                "DiscordBot.ArrayToken..ctor",
                "DiscordBot.SlashCommands.Modules.SlashPermissions.getCmdRow",
                "DiscordBot.Services.PermissionsService.findPerms",
                "DiscordBot.Services.BuiltIn.CmdDisableService.getDisabled",
                "DiscordBot.Services.BuiltIn.ConfigUpdateService.Save",
                "DiscordBot.MLAPI.Handler.Start",
                "DiscordBot.MLAPI.Handler.set_Listening",
                "DiscordBot.MLAPI.Handler.listenLoop",
                "DiscordBot.MLAPI.Replacements.TryGetFieldOrProperty",
                "DiscordBot.MLAPI.Modules.BotPerms.buildHTML",
                "DiscordBot.MLAPI.Modules.Docs.getListInfo",
                "DiscordBot.MLAPI.Modules.Bot.Config.getHtml",
                "DiscordBot.MLAPI.Modules.Bot.Config.getJson",
                "DiscordBot.Commands.Modules.EvalCommand.getJson",
                "DiscordBot.Services.Service.SendClose",
                "DiscordBot.Services.Service.SendSave",
                "DiscordBot.Services.Service.findTrueException",
                "DiscordBot.Services.Service.sendFunction",
                "DiscordBot.Services.CinemaService.purgeOldCache",
                "DiscordBot.Permissions.FieldNodeInfo.getAttrirbs",
                "DiscordBot.Classes.Legislation.LawThing.SetInitialNumber",
                "DiscordBot.Classes.Legislation.LawThing.get_HierarchyId",
                "DiscordBot.Classes.Legislation.LawThing.get_Law",
                "DiscordBot.Classes.Legislation.Amending.AmendedText.get_StartIndex",
                "DiscordBot.Classes.HTMLHelpers.HTMLBase.Write",
                "DiscordBot.Classes.Calculator.CalculationTree.getNode",
                "DiscordBot.Classes.Calculator.ArrayNode..ctor",
                "DiscordBot.Classes.Calculator.OperatorNode..ctor",
                "DiscordBot.Classes.Calculator.FunctionNode..ctor",
                "DiscordBot.Classes.Calculator.BracketNode..ctor",
                "DiscordBot.Classes.Calculator.Calculator.AddStep",
                "DiscordBot.Services.GroupMuteService+GroupGame.Broadcast",
                "DiscordBot.Services.GroupMuteService+GroupGame.SetStates",
                "DiscordBot.Services.GroupMuteService+GroupGame.<SetStates>b__19_0",
                "DiscordBot.Services.GroupMuteService+GroupGame.<Broadcast>b__20_0",
                ""
            };

            // Check every method:
            foreach (var method in calling.Keys)
            {
                var nm = method.DeclaringType.FullName ?? method.DeclaringType.Name ?? "";
                if (nm.StartsWith("DiscordBot.") == false) continue;
                var full = nm + "." + method.Name;
                if (ignored.Contains(full)) continue;
                /*Console.WriteLine($"{method.DeclaringType.FullName}.{method.Name}:");
                foreach (var thing in calling.GetValueOrDefault(method, new MethodBase[0]))
                {
                    Console.WriteLine($"    -> {thing.DeclaringType.FullName}.{thing.Name}");
                }*/
                    // If method A -> ... -> method A, we have a possible infinite recursion
                    var x = CheckRecursion(method, calling, new HashSet<MethodBase>());
                if(x != null)
                {
                    var fg = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"!!! Recursion:");
                    var n = $"{method.DeclaringType.FullName}.{method.Name}";
                    Console.WriteLine($"{n} -> ... -> {x.DeclaringType.FullName}.{x.Name} -> {method.Name}");
                    WindowsClipboard.SetText(n);
                    Console.ForegroundColor = fg;
                    Console.ReadLine();
                }
                //Console.ReadLine();
            }
        }

        static class WindowsClipboard
        {
            public static void SetText(string text)
            {
                OpenClipboard();

                EmptyClipboard();
                IntPtr hGlobal = default;
                try
                {
                    var bytes = (text.Length + 1) * 2;
                    hGlobal = Marshal.AllocHGlobal(bytes);

                    if (hGlobal == default)
                    {
                        ThrowWin32();
                    }

                    var target = GlobalLock(hGlobal);

                    if (target == default)
                    {
                        ThrowWin32();
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                    }
                    finally
                    {
                        GlobalUnlock(target);
                    }

                    if (SetClipboardData(cfUnicodeText, hGlobal) == default)
                    {
                        ThrowWin32();
                    }

                    hGlobal = default;
                }
                finally
                {
                    if (hGlobal != default)
                    {
                        Marshal.FreeHGlobal(hGlobal);
                    }

                    CloseClipboard();
                }
            }

            public static void OpenClipboard()
            {
                var num = 10;
                while (true)
                {
                    if (OpenClipboard(default))
                    {
                        break;
                    }

                    if (--num == 0)
                    {
                        ThrowWin32();
                    }

                    Thread.Sleep(100);
                }
            }

            const uint cfUnicodeText = 13;

            static void ThrowWin32()
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr GlobalLock(IntPtr hMem);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool GlobalUnlock(IntPtr hMem);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool CloseClipboard();

            [DllImport("user32.dll", SetLastError = true)]
            static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

            [DllImport("user32.dll")]
            static extern bool EmptyClipboard();
        }


        private static MethodBase CheckRecursion(MethodBase method, Dictionary<MethodBase, MethodBase[]> calling, HashSet<MethodBase> done)
        {
            var lookingAt = new Stack<MethodBase>();
            foreach(var x in calling.GetValueOrDefault(method, new MethodBase[0]))
            {
                lookingAt.Push(x);
                done.Add(x);
            }
            Console.WriteLine($"Attempting to find {method.DeclaringType.FullName}.{method.Name} in {lookingAt.Count} child calls");
            while (lookingAt.Count > 0)
            {
                var current = lookingAt.Pop();
                var calls = calling.GetValueOrDefault(current, new MethodBase[0]);
                Console.WriteLine($"  Looking at {current.DeclaringType.Name}.{current.Name} with {calls.Length} child calls");
                foreach (var x in calls)
                {
                    Console.WriteLine($"    -> {x.DeclaringType.Name}.{x.Name}");
                    if (x.Equals(method))
                    {
                        Console.WriteLine($"      - Recursed.");
                        return current;
                    }
                    if (!done.Contains(x))
                    {
                        Console.WriteLine($"      - Pushed to stack; {lookingAt.Count}");
                        lookingAt.Push(x);
                    }
                }
                done.Add(current);
            }

            return null;
        }
    }
}
