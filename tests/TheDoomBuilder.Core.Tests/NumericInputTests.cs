using CodeImp.DoomBuilder.Controls;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class NumericInputTests
{
    private static NumericInput Make(string text, bool relative = true, bool expressions = false, bool negative = true)
        => new NumericInput(text) { AllowRelative = relative, AllowExpressions = expressions, AllowNegative = negative };

    [Fact] public void A_plain_number_replaces_the_original() => Assert.Equal(64, Make("64").GetResultFloat(10));
    [Fact] public void Empty_text_keeps_the_original() => Assert.Equal(10, Make("").GetResultFloat(10));
    [Fact] public void Double_plus_adds_and_double_minus_subtracts()
    {
        Assert.Equal(15, Make("++5").GetResultFloat(10));
        Assert.Equal(5, Make("--5").GetResultFloat(10));
    }
    [Fact] public void Triple_plus_grows_with_each_element()
    {
        var input = Make("+++8");
        Assert.Equal(18, input.GetResultFloat(10));   // first element: +8
        Assert.Equal(36, input.GetResultFloat(20));   // second: +16
        input.ResetIncrementStep();
        Assert.Equal(18, input.GetResultFloat(10));
    }
    [Fact] public void Multiply_and_divide_prefixes()
    {
        Assert.Equal(25, Make("*2.5").GetResultFloat(10));
        Assert.Equal(4, Make("/2.5").GetResultFloat(10));
        Assert.Equal(10, Make("/0").GetResultFloat(10));   // division by zero keeps the original
    }
    [Fact] public void Without_negatives_a_result_below_zero_keeps_the_original()
    {
        Assert.Equal(3, Make("--5", negative: false).GetResultFloat(3));
        Assert.Equal(3, Make("-5", negative: false).GetResultFloat(3));
    }
    [Fact] public void Expressions_are_evaluated()
    {
        Assert.Equal(480, Make("(128+64)*2.5", expressions: true).GetResultFloat(0));
        Assert.False(Make("12+", expressions: true).IsValid);
        Assert.True(Make("++", expressions: true).IsValid);
    }
    [Fact] public void Relative_detection_needs_a_value_after_the_prefix()
    {
        Assert.True(Make("++5").IsRelative);
        Assert.False(Make("++").IsRelative);
        Assert.True(Make("*2").IsRelative);
        Assert.False(Make("5").IsRelative);
    }
}
