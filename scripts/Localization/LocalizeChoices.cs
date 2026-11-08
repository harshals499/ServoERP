using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class LocalizeChoices
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
        output.Save(Path.Combine(root,"artifacts","marathi-localization","extracted-choice-keys.xml"));
        Console.WriteLine($"Rewrote {edits} assignments/initializers in {files} files; {Keys.Count} presentation keys."); return 0;
    }
    sealed class Rewrite : CSharpSyntaxRewriter
    {
        readonly SemanticModel Model;
        internal int Edits;
        internal Rewrite(SemanticModel model) { Model = model; }
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbol = Model.GetSymbolInfo(node).Symbol as IPropertySymbol;
            if (symbol?.Name == "Text" && symbol.ContainingType.ToDisplayString() == "System.Windows.Forms.ComboBox" &&
                !(node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node) &&
                !(node.Parent is ArgumentSyntax argument && argument.Parent?.Parent is InvocationExpressionSyntax invocation && invocation.Expression.ToString() == "nameof"))
            {
                Edits++;
                return SyntaxFactory.ParseExpression(LM + "ReadChoiceText(" + node.Expression + ")").WithTriviaFrom(node);
            }
            return base.VisitMemberAccessExpression(node);
        }
        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var property = Model.GetSymbolInfo(node.Left).Symbol as IPropertySymbol;
            if (property != null && node.Right is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string owner = property.ContainingType.ToDisplayString();
                if ((owner == "System.Windows.Forms.FileDialog" && property.Name == "Title") ||
                    (owner == "System.Windows.Forms.FolderBrowserDialog" && property.Name == "Description"))
                {
                    Keys.Add(literal.Token.ValueText); Edits++;
                    return node.WithRight(SyntaxFactory.ParseExpression(LM + "Get(" + literal + ")").WithTriviaFrom(node.Right));
                }
            }
            return base.VisitAssignmentExpression(node);
        }
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var method = Model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (method == null || method.ContainingType.ToDisplayString() != "System.Windows.Forms.ComboBox.ObjectCollection" ||
                !(node.Expression is MemberAccessExpressionSyntax call) || !(call.Expression is MemberAccessExpressionSyntax collection) || collection.Name.Identifier.ValueText != "Items") return base.VisitInvocationExpression(node);
            if (method.Name == "Add" && node.ArgumentList.Arguments.Count == 1 && node.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax item && item.Initializer != null)
            {
                var fields = item.Initializer.Expressions.OfType<AssignmentExpressionSyntax>().ToArray();
                var caption = fields.FirstOrDefault(field => field.Left.ToString() == "Text" && field.Right is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression));
                bool placeholder = fields.Any(field => field.Left.ToString() == "Id" && field.Right is LiteralExpressionSyntax id && id.Token.ValueText == "0");
                if (caption != null && placeholder)
                {
                    Keys.Add(((LiteralExpressionSyntax)caption.Right).Token.ValueText); Edits++;
                    return SyntaxFactory.ParseExpression(LM + "AddChoice(" + collection.Expression + ", " + item + ", " + caption.Right + ")").WithTriviaFrom(node);
                }
            }
            ExpressionSyntax[] choices;
            if (method.Name == "Add" && node.ArgumentList.Arguments.Count == 1 && node.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax)
                choices = new[] { node.ArgumentList.Arguments[0].Expression };
            else if (method.Name == "AddRange" && node.ArgumentList.Arguments.Count == 1 && node.ArgumentList.Arguments[0].Expression is ArrayCreationExpressionSyntax array && array.Initializer != null)
                choices = array.Initializer.Expressions.ToArray();
            else if (method.Name == "AddRange" && node.ArgumentList.Arguments.Count == 1 && node.ArgumentList.Arguments[0].Expression is ImplicitArrayCreationExpressionSyntax implicitArray)
                choices = implicitArray.Initializer.Expressions.ToArray();
            else return base.VisitInvocationExpression(node);
            if (choices.Length == 0 || choices.Any(choice => !(choice is LiteralExpressionSyntax literal) || !literal.IsKind(SyntaxKind.StringLiteralExpression))) return base.VisitInvocationExpression(node);
            foreach (LiteralExpressionSyntax choice in choices) Keys.Add(choice.Token.ValueText);
            string helper = method.Name == "Add" ? "AddChoice" : "AddChoices";
            Edits++;
            return SyntaxFactory.ParseExpression(LM + helper + "(" + collection.Expression + ", " + node.ArgumentList.Arguments[0].Expression + ")").WithTriviaFrom(node);
        }
    }
}
