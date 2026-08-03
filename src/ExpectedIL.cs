using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;

namespace mcevilslug;

public record ExpectedIL
{
    public HashSet<OpCode> ValidCodes;
    public Func<Instruction, bool> Predicate;

    public ExpectedIL(HashSet<OpCode> codes, Func<Instruction, bool> predicate)
    {
        ValidCodes = codes;
        Predicate = predicate;
    }
}