using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace mcevilslug;

public static class ILDebug
{
    public static string IterateEntireILContext(ILContext il, ExpectedIL[] instructions, bool discardIrrelevant = true)
    {
        if (instructions.Length == 0)
        {
            return "Instructions array was empty!";
        }
        
        ILCursor c = new ILCursor(il);
        Func<Instruction, bool> startInstruction = instructions[0].Predicate;
        StringBuilder results = new();
        string attempt = null;

        while (c.TryGotoNext(MoveType.Before, startInstruction))
        {
            int start = c.Index;

            attempt = IterateILFromPoint(c, instructions, discardIrrelevant);

            if (!string.IsNullOrEmpty(attempt))
            {
                results.AppendLine(attempt);
                results.AppendLine();
            }

            c.Index = start + 1;
        }

        if (results.Length == 0)
        {
            results.AppendLine("FAIL: Found no plausible matches!");
        }

        return results.ToString();
    }

    public static string IterateILFromPoint(ILCursor c, ExpectedIL[] instructions, bool discardIrrelevant)
    {
        //array has lambdas that return if an instruction matches the expected outcome
        
        if (instructions.Length == 0)
        {
            return "Instructions array was empty!";
        }
        
        int start = c.Index;
        StringBuilder results = new();
        
        //write first line of report
        results.AppendLine($"Candidate @ IL index {FormatInstruction(c.Instrs[start])}");
        
        for (int i = 0; i < instructions.Length; i++)
        {
            if (start + i >= c.Instrs.Count) //accounts for incorrect expected number of instructions
            {
                results.AppendLine($"{i} END OF METHOD");
                break;
            }

            Instruction current = c.Instrs[start + i];
            
            //CHECK IF PREDICATE CALL TYPES MATCH; IF NOT, RETURN EMPTY STRING
            if (discardIrrelevant && !instructions[i].ValidCodes.Contains(current.OpCode))
            {
                return null;
            }
            
            if (!instructions[i].Predicate(current))
            {
                //same OpCode family, but match failed
                
                results.AppendLine($"{i} FAIL {FormatInstruction(current)}");
            
                //print nearby instructions for debugging
                for (int k = 0; k < 4 && start + i + k < c.Instrs.Count; k++)
                {
                    results.AppendLine($"{FormatInstruction(c.Instrs[start + i + k])}");
                }
                results.AppendLine();
                break;
            }
                
            results.AppendLine($"{i} PASS {FormatInstruction(current)}");
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