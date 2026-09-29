using empifisJsonAPI2;
using Xunit;

namespace empifisJsonService2.Tests;

public class ErrorCodesTests
{
    [Theory]
    [InlineData(27, "ERR_DEFICIENT_PAYMENT")]
    [InlineData(557, "interrupted by a service restart")]
    [InlineData(998, "not supported by the installed EmpiFisX.dll")]
    [InlineData(10051, "Card terminal (ECR) error 10051")]
    [InlineData(12345678, "Error 12345678.")]
    public void Describe_ReturnsTheManualDescription(int errorCode, string expected)
    {
        Assert.Contains(expected, ErrorCodes.Describe(errorCode));
    }
}
