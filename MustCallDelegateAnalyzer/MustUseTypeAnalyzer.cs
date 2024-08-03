using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MustUseTypeAnalyzer : DiagnosticAnalyzer
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
            if (HasMustUseAttribute(parameterType))
            {
                if (!IsParameterProperlyUsedOrPassed(methodDeclaration, parameterSymbol, semanticModel))
                {
                    var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }
            }
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
            .Where(id => semanticModel.GetSymbolInfo(id).Symbol?.Equals(parameterSymbol) == true);

        return parameterUsages.Any(usage => IsProperlyUsed(usage, semanticModel) || IsPassedToMethod(usage));
    }

    private bool IsProperlyUsed(IdentifierNameSyntax identifierName, SemanticModel semanticModel)
    {
        var parent = identifierName.Parent;

        // Check if it's invoked
        if (parent is InvocationExpressionSyntax)
            return true;

        // Check if a member is accessed
        if (parent is MemberAccessExpressionSyntax)
            return true;

        // Check if it's used in a way that's not just assignment or discard
        if (parent is AssignmentExpressionSyntax assignment)
        {
            // If it's on the right side of the assignment, it's not considered proper use
            return assignment.Left == identifierName;
        }

        // Check if it's not just discarded
        if (parent is EqualsValueClauseSyntax equalsValue &&
            equalsValue.Parent is VariableDeclaratorSyntax variableDeclarator &&
            variableDeclarator.Identifier.Text == "_")
        {
            return false;
        }

        // Add more checks here as needed

        // For any other usage, we'll consider it properly used
        return true;
    }

    private bool IsPassedToMethod(IdentifierNameSyntax identifierName)
    {
        return identifierName.Parent is ArgumentSyntax;
    }
}