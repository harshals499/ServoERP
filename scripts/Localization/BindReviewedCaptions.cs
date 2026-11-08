using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class BindReviewedCaptions
{
    internal const string LM = "global::HVAC_Pro_Desktop.Services.LanguageManager.";
    internal static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);
    static int Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]), project=Path.Combine(root,"SOURCE_CODE","HVAC_Pro_Desktop.csproj");
        var xml=XDocument.Load(project); XNamespace ns=xml.Root.Name.Namespace;
        var paths=xml.Descendants(ns+"Compile").SelectMany(n=>n.Attribute("Include").Value.Split(';')).Select(p=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project),p))).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var trees=paths.Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),new CSharpParseOptions(LanguageVersion.Latest),p)).ToArray();
        string framework=@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2";
        var references=Directory.GetFiles(framework,"*.dll").Concat(Directory.GetFiles(Path.Combine(framework,"Facades"),"*.dll")).ToList();
        references.AddRange(xml.Descendants(ns+"Reference").Select(n=>n.Element(ns+"HintPath")?.Value).Where(v=>v!=null).Select(p=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project),p))).Where(File.Exists));
        var compilation=CSharpCompilation.Create("LocalizationAnalysis",trees,references.Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>MetadataReference.CreateFromFile(p)),new CSharpCompilationOptions(OutputKind.WindowsApplication));
        int files=0, edits=0;
        foreach(var tree in trees)
        {
            string relative=tree.FilePath.Substring(root.Length).Replace('\\','/');
            if (!(relative.StartsWith("/SOURCE_CODE/UI/",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("/SOURCE_CODE/Infrastructure/",StringComparison.OrdinalIgnoreCase))) continue;
            var rewriter=new Rewrite(compilation.GetSemanticModel(tree)); var before=tree.GetRoot(); var after=rewriter.Visit(before);
            if(rewriter.Edits>0 && !(args.Length>1 && args[1]=="--audit")) {File.WriteAllText(tree.FilePath,after.ToFullString(),new System.Text.UTF8Encoding(true));files++;edits+=rewriter.Edits;}
        }
        var output=new XElement("root",Keys.OrderBy(k=>k,StringComparer.Ordinal).Select(k=>new XElement("data",new XAttribute("name",k),new XElement("value",k))));
        output.Save(Path.Combine(root,"artifacts","marathi-localization","bound-caption-keys.xml"));
        Console.WriteLine($"Rewrote {edits} assignments/initializers in {files} files; {Keys.Count} presentation keys."); return 0;
    }
    sealed class Rewrite : CSharpSyntaxRewriter
    {
        readonly SemanticModel Model;
        static readonly HashSet<string> Approved = new HashSet<string>(File.ReadAllLines("scripts/Localization/reviewed-caption-bindings.txt"));
        internal int Edits;
        internal Rewrite(SemanticModel model) { Model = model; }
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var method = Model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            var callOwner = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            bool alreadyBound = node.Parent is ArgumentSyntax argument && argument.Parent?.Parent is InvocationExpressionSyntax parent && parent.Expression.ToString().EndsWith("LanguageManager.Caption");
            if (method != null && callOwner != null && !alreadyBound && node.ArgumentList.Arguments.Any(arg => Approved.Contains(Path.GetFileName(Model.SyntaxTree.FilePath) + "|" + callOwner.Identifier.ValueText + "|@" + method.Name + ":" + arg.Expression)))
            {
                Edits++;
                return SyntaxFactory.ParseExpression(LM + "Caption(" + node.WithoutTrivia().ToFullString() + ")").WithTriviaFrom(node);
            }
            if (method?.ContainingType.ToDisplayString() != "HVAC_Pro_Desktop.Services.LanguageManager" || method.Name != "DataText" || node.ArgumentList.Arguments.Count != 1 || !(node.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax control)) return base.VisitInvocationExpression(node);
            var text = control.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().FirstOrDefault(field => field.Left.ToString() == "Text");
            var owner = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (text != null && text.Right.ToString() == "text" && owner != null && new[] { "MakeCell", "MakePayCell", "MakePoCell", "MakeTableLabel" }.Contains(owner.Identifier.ValueText) && owner.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "header"))
            {
                Edits++;
                return node.WithExpression(SyntaxFactory.ParseExpression(LM + "TableCell")).WithArgumentList(node.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("header"))));
            }
            if (text == null || owner == null || !Approved.Contains(Path.GetFileName(Model.SyntaxTree.FilePath) + "|" + owner.Identifier.ValueText + "|" + text.Right)) return base.VisitInvocationExpression(node);
            Edits++;
            return node.WithExpression(SyntaxFactory.ParseExpression(LM + "Caption"));
        }
    }
}
