using empifisJsonAPI2;
using Xunit;

namespace empifisJsonService2.Tests;

public class ErrorCodesTests
{
    [Theory]
    [InlineData(27, "ERR_DEFICIENT_PAYMENT")]
    [InlineData(557, "interrupted by a service restart")]
    [InlineData(998, "not supported by the installed EmpiFisX.dll")]
    [InlineData(10051, "Card terminal (ECR) error ECR_WL_P051: 51 - Insufficient funds")]
    [InlineData(10949, "ECR_WL_P949")]
    [InlineData(15999, "Card terminal (ECR) error 15999")]
    [InlineData(12345678, "Error 12345678.")]
    public void Describe_ReturnsTheManualDescription(int errorCode, string expected)
    {
        Assert.Contains(expected, ErrorCodes.Describe(errorCode));
    }
}
