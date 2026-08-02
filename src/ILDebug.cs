using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace mcevilslug;

public static class ILDebug
{
    public static string IterateEntireILContext(ILContext il, params Func<Instruction, bool>[] instructions)
    {
        if (instructions.Length == 0)
        {
            return "Instructions array was empty!";
        }
        
        ILCursor c = new ILCursor(il);
        Func<Instruction, bool> startInstruction = instructions[0];
        StringBuilder results = new();

        bool foundAnyMatches = false;

        while (c.TryGotoNext(MoveType.Before, startInstruction))
        {
            foundAnyMatches = true;
            
            int start = c.Index;

            results.AppendLine(IterateILFromPoint(c, instructions));

            c.Index = start + 1;
        }
        
        if (!foundAnyMatches)
        {
            results.AppendLine("FAIL: No matches found for the first instruction!");
        }

        return results.ToString();
    }

    public static string IterateILFromPoint(ILCursor c, params Func<Instruction, bool>[] instructions)
    {
        if (instructions.Length == 0)
        {
            return "Instructions array was empty!";
        }
        
        int start = c.Index;
        StringBuilder results = new();
        
        results.AppendLine($"Candidate @ IL index {FormatInstruction(c.Instrs[start])}");
        
        for (int i = 0; i < instructions.Length; i++)
        {
            if (start + i >= c.Instrs.Count)
            {
                results.AppendLine($"{i} END OF METHOD");
                break;
            }
                
            if (!instructions[i](c.Instrs[start + i]))
            {
                results.AppendLine($"{i} FAIL {FormatInstruction(c.Instrs[start + i])}");
                for (int k = 0; k < 4 && start + i + k < c.Instrs.Count; k++)
                {
                    results.AppendLine($"{FormatInstruction(c.Instrs[start + i + k])}");
                }
                results.AppendLine();
                break;
            }
                
            results.AppendLine($"{i} PASS {FormatInstruction(c.Instrs[start + i])}");
        }
        
        return results.ToString();
    }
    
    private static string FormatInstruction(Instruction instruction)
    {
        if (instruction == null)
        {
            return "<null>";
        }

        string operand;
        
        try
        {
            operand = instruction.Operand?.ToString() ?? "";
        }
        catch
        {
            operand = $"<unprintable operand>";
        }
        
        return $"IL_{instruction.Offset:X4}: {instruction.OpCode} {operand}";
    }
}