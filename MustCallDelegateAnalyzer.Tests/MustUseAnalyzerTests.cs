using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace MustCallDelegateAnalyzer.Tests;

public class MustUseAnalyzerTests
{
    private readonly DiagnosticResult _result = new DiagnosticResult(MustUseTypeAnalyzer.Rule)
        .WithArguments("callback");
    
    [Fact]
    public async Task AnalyzerTriggers_WhenMustUseType_IsArgToEmptyMethod()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
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

        var result = _result.WithSpan(11, 28, 11, 54);
        
        context.ExpectedDiagnostics.Add(result);
        
        await context.RunAsync();
    }
    
    [Fact]
    public async Task AnalyzerDoesNotTrigger_WhenMustUseType_IsInvokedAsMethod()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
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
                               callback();
                           }
                       }
                       """,
        };

        await context.RunAsync();
    }
    
    [Fact]
    public async Task AnalyzerDoesNotTrigger_WhenMustUseType_IsInvokedExplicitly()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
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
                               callback.Invoke();
                           }
                       }
                       """,
        };

        await context.RunAsync();
    }
    
    [Fact]
    public async Task AnalyzerDoesNotTrigger_WhenMustUseType_IsPassedToOtherMethod_AndThenInvoked()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       using System;

                       [AttributeUsage(AttributeTargets.Delegate)]
                       public class MustUseAttribute : Attribute { }

                       [MustUse]
                       public delegate void ImportantCallback();

                       public class TestClass
                       {
                           public void PassCallback(ImportantCallback callback)
                           {
                               InvokeCallback(callback);
                           }
                           
                           public void InvokeCallback(ImportantCallback callback)
                           {
                               callback.Invoke();
                           }
                       }
                       """,
        };

        await context.RunAsync();
    }
    
    [Fact]
    public async Task AnalyzerTriggers_WhenMustUseType_IsPassedToOtherMethod_AndThenNotInvoked()
    {
        var context = new CSharpAnalyzerTest<MustUseTypeAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       using System;

                       [AttributeUsage(AttributeTargets.Delegate)]
                       public class MustUseAttribute : Attribute { }

                       [MustUse]
                       public delegate void ImportantCallback();

                       public class TestClass
                       {
                           public void PassCallback(ImportantCallback callback)
                           {
                               InvokeCallback(callback);
                           }
                           
                           public void InvokeCallback(ImportantCallback callback)
                           {
                               
                           }
                       }
                       """,
        };

        var result = _result.WithSpan(16, 32, 16, 58);
        
        context.ExpectedDiagnostics.Add(result);
        
        await context.RunAsync();
    }
}