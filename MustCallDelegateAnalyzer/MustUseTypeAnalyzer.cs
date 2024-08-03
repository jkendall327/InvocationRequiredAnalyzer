using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MustCallDelegateAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MustUseTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MustUseType";

    private static readonly LocalizableString Title = "MustUse type not used";
    private static readonly LocalizableString MessageFormat = "The parameter '{0}' of type marked with [MustUse] was not used or passed to another method";
    private static readonly LocalizableString Description = "Parameters of types marked with [MustUse] should be used within the method or passed to another method.";
    private const string Category = "Usage";

    internal static readonly DiagnosticDescriptor Rule = new(DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
            
            if (!HasMustUseAttribute(parameterType) || IsParameterUsedOrPassed(methodDeclaration, parameterSymbol, semanticModel))
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool HasMustUseAttribute(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "MustUseAttribute");
    }

    private bool IsParameterUsedOrPassed(MethodDeclarationSyntax methodDeclaration, IParameterSymbol parameterSymbol, SemanticModel semanticModel)
    {
        var parameterUsages = methodDeclaration.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => semanticModel.GetSymbolInfo(id).Symbol?.Equals(parameterSymbol) == true);

        return parameterUsages.Any(usage => IsUsed(usage) || IsPassedToMethod(usage));
    }

    private bool IsUsed(IdentifierNameSyntax identifierName)
    {
        // This is a simplified check. You might want to expand this based on your specific requirements.
        return !(identifierName.Parent is ArgumentSyntax);
    }

    private bool IsPassedToMethod(IdentifierNameSyntax identifierName)
    {
        return identifierName.Parent is ArgumentSyntax;
    }
}