using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using OpCode = Mono.Cecil.Cil.OpCode;

namespace mcevilslug;

public record ExpectedIL
{
    public HashSet<OpCode> ValidCodes;
    public Func<Instruction, bool> Predicate;
    [CanBeNull] public Type ObjectType;

    public ExpectedIL(HashSet<OpCode> codes, Func<Instruction, bool> predicate, Type type = null)
    {
        ValidCodes = codes;
        Predicate = predicate;
        ObjectType = type;
    }
}