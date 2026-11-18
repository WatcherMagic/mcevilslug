using System;
using System.Text;
using Mono.Cecil;
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

    public static string IterateILFromPoint(ILCursor c, ExpectedIL[] instructions, bool discardIrrelevant = true)
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

            if (instructions[i].ObjectType != null)
            {
                //convert system.reflection type to monomod.cil.cecil typereference and compare
                TypeReference expectedType = c.Context.Import(instructions[i].ObjectType);
                if (!MatchesDeclaringType(current, expectedType))
                {
                    return null;
                }
            }
            
            if (!instructions[i].Predicate(current))
            {
                //same OpCode family, but match failed
                
                results.AppendLine($"{i} FAIL {FormatInstruction(current)}");
            
                //print nearby instructions for debugging
                results.AppendLine(PrintInstructionsFromPoint(c, 4));
                results.AppendLine();
                break;
            }
                
            results.AppendLine($"{i} PASS {FormatInstruction(current)}");

            if (i == instructions.Length - 1)
            {
                results.AppendLine("FULL MATCH!");
                results.AppendLine(PrintInstructionsFromPoint(c, 4));
            }
        }
        
        return results.ToString();
    }

    private static bool MatchesDeclaringType(Instruction instr, TypeReference expected)
    {
        TypeReference actual = instr.Operand switch
        {
            FieldReference field => field.DeclaringType,
            MethodReference method => method.DeclaringType,
            TypeReference type => type,
            _ => null
        };
        
        return SameType(actual, expected);
    }

    private static bool SameType(TypeReference actual, TypeReference expected)
    {
        return NormalizeFullNames(actual.FullName) == NormalizeFullNames(expected.FullName) && actual.Scope?.Name == expected.Scope?.Name;
    }

    private static string NormalizeFullNames(string name)
    {
        return name.Replace("/", "+");
    }
    
    public static string PrintInstructionsFromPoint(ILCursor c, int numLines)
    {
        StringBuilder results = new();

        for (int i = 0; i < numLines; i++)
        {
            results.AppendLine($"{i} {FormatInstruction(c.Next)}");
            
            if (c.Index >= c.Instrs.Count) //accounts for incorrect expected number of instructions
            {
                results.AppendLine($"{i} END OF METHOD");
                break;
            }
            c.Index++;
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