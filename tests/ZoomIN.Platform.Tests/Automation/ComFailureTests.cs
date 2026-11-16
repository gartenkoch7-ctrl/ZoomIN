using System.Runtime.InteropServices;
using ZoomIN.Platform.Automation;

namespace ZoomIN.Platform.Tests.Automation;

/// <summary>
/// Every failed HRESULT another application can answer with is caught, whatever .NET exception
/// interop turns it into – E_INVALIDARG from a Chromium window ended ZoomIN on 2026-11-09.
/// </summary>
public sealed class ComFailureTests
{
    [Theory]
    [InlineData(unchecked((int)0x80070057))] // E_INVALIDARG -> ArgumentException
    [InlineData(unchecked((int)0x80004002))] // E_NOINTERFACE -> InvalidCastException
    [InlineData(unchecked((int)0x80004001))] // E_NOTIMPL -> NotImplementedException
    [InlineData(unchecked((int)0x80070005))] // E_ACCESSDENIED -> UnauthorizedAccessException
    [InlineData(unchecked((int)0x80004005))] // E_FAIL -> COMException
    [InlineData(unchecked((int)0x80040201))] // UIA_E_ELEMENTNOTAVAILABLE -> COMException
    public void FailedHResults_AreAnswers(int hresult)
    {
        var exception = Marshal.GetExceptionForHR(hresult)!;

        Assert.True(ComFailure.IsAnswer(exception), exception.GetType().Name);
    }

    [Fact]
    public void OwnBugs_AreNoAnswers() =>
        Assert.False(ComFailure.IsAnswer(new InvalidOperationException()));
}
