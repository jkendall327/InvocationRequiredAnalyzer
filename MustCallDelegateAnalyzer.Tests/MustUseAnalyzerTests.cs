using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace MustCallDelegateAnalyzer.Tests;

public class MustUseAnalyzerTests
{
    [Fact]
    public async Task AnalyzerTriggers_WhenMustUseType_IsArgToEmptyMethod()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            
            TestCode = """
                       using System;
                       
                       [AttributeUsage(AttributeTargets.Delegate)]
                       public class MustUseAttribute : Attribute { }
                       
                       [MustUse]
                       public delegate void ImportantCallback();
                       
                       public class TestClass
                       {
                           public void TestMethod(ImportantCallback callback)
                           {
                               // Parameter is not used or passed
                           }
                       }
                       """,
        };

        var result = new DiagnosticResult(MustUseTypeAnalyzer.Rule)
            .WithArguments("callback")
            .WithSpan(11, 28, 11, 54);
        
        context.ExpectedDiagnostics.Add(result);
        
        await context.RunAsync();
    }
}