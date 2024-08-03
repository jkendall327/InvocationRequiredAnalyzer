using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MustCallDelegateAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InvocationRequiredAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MustUseType";

    private static readonly LocalizableString Title = "MustUse type not properly used";
    private static readonly LocalizableString MessageFormat = "The parameter '{0}' of type marked with [MustUse] was not properly used or passed to another method";
    private static readonly LocalizableString Description = "Parameters of types marked with [MustUse] should be invoked, have their members accessed, or be passed to another method.";
    private const string Category = "Usage";

    internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        foreach (var parameter in methodDeclaration.ParameterList.Parameters)
        {
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
            
            if (parameterSymbol == null) continue;

            var parameterType = parameterSymbol.Type;
            
            if (!HasMustUseAttribute(parameterType)) continue;
            
            if (IsParameterProperlyUsedOrPassed(methodDeclaration, parameterSymbol, semanticModel)) continue;
            
            var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text);
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool HasMustUseAttribute(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "MustUseAttribute");
    }

    private bool IsParameterProperlyUsedOrPassed(MethodDeclarationSyntax methodDeclaration, IParameterSymbol parameterSymbol, SemanticModel semanticModel)
    {
        var parameterUsages = methodDeclaration.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(id).Symbol, parameterSymbol));

        return parameterUsages.Any(usage => IsProperlyUsed(usage, semanticModel) || IsPassedToMethod(usage));
    }

    private bool IsProperlyUsed(IdentifierNameSyntax identifierName, SemanticModel semanticModel)
    {
        var parent = identifierName.Parent;
        
        // Check if it's invoked implicitly (i.e. myfunc()).
        if (parent is InvocationExpressionSyntax)
        {
            return true;
        }

        // Check if it's invoked via the .Invoke() method.
        if (parent is not MemberAccessExpressionSyntax memberAccess) return false;
        
        if (memberAccess.Name.Identifier.Text != "Invoke") return false;
            
        var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            
        return symbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke };
    }

    private bool IsPassedToMethod(IdentifierNameSyntax identifierName)
    {
        return identifierName.Parent is ArgumentSyntax;
    }
}