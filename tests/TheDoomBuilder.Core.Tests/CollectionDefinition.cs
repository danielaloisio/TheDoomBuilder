using Xunit;

namespace DoomBuilder.Core.Tests;

// The Core still keeps global state in the static General class, so tests that touch it must not run in parallel.
[CollectionDefinition("General static state", DisableParallelization = true)]
public class GeneralStateCollection { }
