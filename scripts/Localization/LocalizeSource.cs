using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class LocalizeSource
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
        output.Save(Path.Combine(root,"artifacts","marathi-localization","extracted-template-keys.xml"));
        Console.WriteLine($"Rewrote {edits} assignments/initializers in {files} files; {Keys.Count} presentation keys."); return 0;
    }
    static bool IsType(ITypeSymbol type,string name) { for(var t=type as INamedTypeSymbol;t!=null;t=t.BaseType) if(t.ToDisplayString()==name)return true;return false; }
    static bool Editable(ITypeSymbol type) => IsType(type,"System.Windows.Forms.TextBoxBase")||IsType(type,"System.Windows.Forms.ListControl")||IsType(type,"System.Windows.Forms.UpDownBase")||IsType(type,"System.Windows.Forms.DateTimePicker")||IsType(type,"System.Windows.Forms.DataGridView")||IsType(type,"HVAC_Pro_Desktop.UI.Controls.ModernTextBox")||IsType(type,"HVAC_Pro_Desktop.UI.Controls.ModernERPSearchBox");
    static string Literal(string value) => SymbolDisplay.FormatLiteral(value,true);
    sealed class Rewrite:CSharpSyntaxRewriter
    {
        readonly SemanticModel Model; public int Edits;
        public Rewrite(SemanticModel model) {Model=model;}
        bool PureKey(ExpressionSyntax expression)
        {
            if(expression is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.StringLiteralExpression)) {Keys.Add(l.Token.ValueText);return true;}
            if(expression is ConditionalExpressionSyntax c)return PureKey(c.WhenTrue)&&PureKey(c.WhenFalse);
            return false;
        }
        string Render(ExpressionSyntax expression)
        {
            if(expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) {string key=literal.Token.ValueText;Keys.Add(key);return LM+"Get("+Literal(key)+")";}
            if(expression is ConditionalExpressionSyntax condition) return "("+condition.Condition+" ? "+Render(condition.WhenTrue)+" : "+Render(condition.WhenFalse)+")";
            if(expression is InterpolatedStringExpressionSyntax interpolation)
            {
                string key=""; var values=new List<string>();
                foreach(var content in interpolation.Contents)
                {
                    if(content is InterpolatedStringTextSyntax text)key+=text.TextToken.ValueText.Replace("{","{{").Replace("}","}}");
                    else if(content is InterpolationSyntax slot){key+="{"+values.Count+(slot.AlignmentClause?.ToString()??"")+(slot.FormatClause?.ToString()??"")+"}";values.Add(slot.Expression.ToString());}
                }
                if(!key.Any(char.IsLetter))return expression.ToString();
                Keys.Add(key);return LM+"Format("+Literal(key)+(values.Count>0?", "+string.Join(", ",values):"")+")";
            }
            if(expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && Model.GetTypeInfo(expression).Type?.SpecialType==SpecialType.System_String)
            {
                var parts=new List<ExpressionSyntax>();Flatten(expression,parts);
                if(!parts.OfType<LiteralExpressionSyntax>().Any(p=>p.IsKind(SyntaxKind.StringLiteralExpression)&&p.Token.ValueText.Any(char.IsLetter)))return expression.ToString();
                string key="";var values=new List<string>();
                foreach(var part in parts)
                    if(part is LiteralExpressionSyntax text && text.IsKind(SyntaxKind.StringLiteralExpression))key+=text.Token.ValueText.Replace("{","{{").Replace("}","}}");
                    else{key+="{"+values.Count+"}";values.Add(part.ToString());}
                Keys.Add(key);return LM+"Format("+Literal(key)+(values.Count>0?", "+string.Join(", ",values):"")+")";
            }
            return expression.ToString();
        }
        void Flatten(ExpressionSyntax expression,List<ExpressionSyntax> parts)
        {
            if(expression is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.AddExpression)&&Model.GetTypeInfo(expression).Type?.SpecialType==SpecialType.System_String){Flatten(b.Left,parts);Flatten(b.Right,parts);}else parts.Add(expression);
        }
        static readonly HashSet<string> FactoryNames=new HashSet<string> {
            "PlaceLabeledControl", "PlaceLabeled", "PlaceInput", "AddField", "FieldLabel", "AddFormField", "AddMetric", "AddSummaryCard", "AddModernSettingsCard", "Pair", "AddDetailRow", "InfoRow", "AddBadge", "AddPill", "AddChip", "AddKpi", "AddStat", "DrawCentered", "DrawLeftAligned"
        };
        static readonly HashSet<string> CaptionParameters=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "text", "title", "caption", "label", "labelText", "header", "headerText", "description", "subtitle", "helpText", "placeholder"
        };
        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited=(AssignmentExpressionSyntax)base.VisitAssignmentExpression(node);
            var property=Model.GetSymbolInfo(node.Left).Symbol as IPropertySymbol;
            if(property==null || !(new[]{"HeaderText","ToolTipText","AccessibleName","AccessibleDescription","Placeholder"}.Contains(property.Name)))return visited;
            if(!(IsType(property.ContainingType,"System.Windows.Forms.Control")||IsType(property.ContainingType,"System.Windows.Forms.ToolStripItem")||IsType(property.ContainingType,"System.Windows.Forms.DataGridViewColumn")||IsType(property.ContainingType,"HVAC_Pro_Desktop.UI.Controls.ModernTextBox")))return visited;
            string value=Render(node.Right); if(value==node.Right.ToString())return visited;
            Edits++;return visited.WithRight(SyntaxFactory.ParseExpression(value).WithTriviaFrom(visited.Right));
        }
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited=(InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            var method=Model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if(method==null)return visited;
            bool lm=method.ContainingType.ToDisplayString()=="HVAC_Pro_Desktop.Services.LanguageManager";
            if(lm && new[]{"Get","GetMessage","Format","SetCaption"}.Contains(method.Name))
            {
                int keyIndex=method.Name=="SetCaption"?1:0;
                if(node.ArgumentList.Arguments.Count>keyIndex && node.ArgumentList.Arguments[keyIndex].Expression is LiteralExpressionSyntax key && key.IsKind(SyntaxKind.StringLiteralExpression))Keys.Add(key.Token.ValueText);
                return visited;
            }
            if(lm)return visited;
            bool paint=(method.ContainingType.ToDisplayString()=="System.Drawing.Graphics"&&method.Name=="DrawString") || (method.ContainingType.ToDisplayString()=="System.Windows.Forms.TextRenderer"&&new[]{"DrawText","MeasureText"}.Contains(method.Name));
            bool factory=IsType(method.ReturnType,"System.Windows.Forms.Control")||IsType(method.ReturnType,"System.Windows.Forms.ToolStripItem")||FactoryNames.Contains(method.Name);
            bool alert=method.Name=="Show" && new[]{"System.Windows.Forms.MessageBox","HVAC_Pro_Desktop.Services.LocalizedMessageBox"}.Contains(method.ContainingType.ToDisplayString());
            bool tooltip=method.Name=="SetToolTip" && method.ContainingType.ToDisplayString()=="System.Windows.Forms.ToolTip";
            if(!factory&&!alert&&!tooltip&&!paint)return visited;
            bool captionFactory=IsType(method.ReturnType,"System.Windows.Forms.ButtonBase")||IsType(method.ReturnType,"System.Windows.Forms.Label")||IsType(method.ReturnType,"System.Windows.Forms.ToolStripItem");
            if(captionFactory && node.Parent is ArgumentSyntax boundArgument && boundArgument.Parent?.Parent is InvocationExpressionSyntax binding && binding.Expression.ToString().Contains("LanguageManager.Caption"))return visited;
            bool bindCaption=false;
            var arguments=visited.ArgumentList.Arguments;
            for(int i=0;i<node.ArgumentList.Arguments.Count;i++)
            {
                var original=node.ArgumentList.Arguments[i];int pi=i;
                if(original.NameColon!=null)pi=Array.FindIndex(method.Parameters.ToArray(),p=>p.Name==original.NameColon.Name.Identifier.ValueText);
                if(pi<0||pi>=method.Parameters.Length)continue;
                string name=method.Parameters[pi].Name;
                bool caption=(factory&&CaptionParameters.Contains(name))||(method.Name=="Pair"&&i==0)||(alert&&(name=="text"||name=="caption"))||(tooltip&&name=="caption")||(paint&&(name=="s"||name=="text"));
                if(!caption)continue;
                var raw=original.Expression;
                if(raw is InvocationExpressionSyntax localized && localized.Expression.ToString().EndsWith("LanguageManager.Get") && localized.ArgumentList.Arguments.Count==1 && localized.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax)raw=localized.ArgumentList.Arguments[0].Expression;
                if((captionFactory || method.Name=="AddModernSettingsCard") && raw is LiteralExpressionSyntax rawKey && rawKey.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    Keys.Add(rawKey.Token.ValueText);
                    arguments=arguments.Replace(arguments[i],arguments[i].WithExpression(raw.WithTriviaFrom(arguments[i].Expression)));bindCaption|=captionFactory;Edits++;continue;
                }
                string value=Render(original.Expression);if(value==original.Expression.ToString())continue;
                arguments=arguments.Replace(arguments[i],arguments[i].WithExpression(SyntaxFactory.ParseExpression(value).WithTriviaFrom(arguments[i].Expression)));Edits++;
            }
            visited=visited.WithArgumentList(visited.ArgumentList.WithArguments(arguments));
            if(bindCaption)return SyntaxFactory.ParseExpression(LM+(IsType(method.ReturnType,"System.Windows.Forms.ToolStripItem")?"CaptionItem":"Caption")+"("+visited.WithoutTrivia().ToFullString()+")").WithTriviaFrom(node);
            return visited;
        }

        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if(!(node.Expression is AssignmentExpressionSyntax assignment)||!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))return base.VisitExpressionStatement(node);
            var property=Model.GetSymbolInfo(assignment.Left).Symbol as IPropertySymbol;
            if(property?.Name!="Text"||(!IsType(property.ContainingType,"System.Windows.Forms.Control")&&!IsType(property.ContainingType,"System.Windows.Forms.ToolStripItem")))return base.VisitExpressionStatement(node);
            ITypeSymbol actual=assignment.Left is MemberAccessExpressionSyntax member?Model.GetTypeInfo(member.Expression).Type:property.ContainingType;
            if(Editable(actual))return base.VisitExpressionStatement(node);
            string target=assignment.Left is MemberAccessExpressionSyntax access?access.Expression.ToString():"this";
            bool explicitGet=assignment.Right is InvocationExpressionSyntax get && get.Expression.ToString().EndsWith("LanguageManager.Get") && get.ArgumentList.Arguments.Count==1;
            string method=PureKey(assignment.Right)||explicitGet?"SetCaption":"SetDataText";
            string value=explicitGet?((InvocationExpressionSyntax)assignment.Right).ArgumentList.Arguments[0].Expression.ToString():method=="SetCaption"?assignment.Right.ToString():Render(assignment.Right);
            Edits++;return SyntaxFactory.ParseStatement(LM+method+"("+target+", "+value+");").WithTriviaFrom(node);
        }
        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var type=Model.GetTypeInfo(node).Type;
            if(type?.ToDisplayString()=="System.Drawing.Font" && node.ArgumentList?.Arguments.Count>0 && node.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax face && face.Token.ValueText=="Segoe UI")
            {
                var font=(ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node);
                Edits++;return font.WithArgumentList(font.ArgumentList.WithArguments(font.ArgumentList.Arguments.Replace(font.ArgumentList.Arguments[0],font.ArgumentList.Arguments[0].WithExpression(SyntaxFactory.ParseExpression(LM+"GetUiFontFamily()")))));
            }
            if((!IsType(type,"System.Windows.Forms.Control")&&!IsType(type,"System.Windows.Forms.ToolStripItem"))||Editable(type)||node.Initializer==null)return base.VisitObjectCreationExpression(node);
            var text=node.Initializer.Expressions.OfType<AssignmentExpressionSyntax>().FirstOrDefault(a=>a.Left.ToString()=="Text");
            if(text==null)return base.VisitObjectCreationExpression(node);
            if(node.Parent is ArgumentSyntax existingArgument && existingArgument.Parent?.Parent is InvocationExpressionSyntax existingBinding &&
                (existingBinding.Expression.ToString().Contains("LanguageManager.Caption") || existingBinding.Expression.ToString().Contains("LanguageManager.DataText")))
            { PureKey(text.Right); return base.VisitObjectCreationExpression(node); }
            bool pure=PureKey(text.Right);var visited=(ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node);
            if(!pure)
            {
                var visitedText=visited.Initializer.Expressions.OfType<AssignmentExpressionSyntax>().First(a=>a.Left.ToString()=="Text");
                visited=visited.ReplaceNode(visitedText,visitedText.WithRight(SyntaxFactory.ParseExpression(Render(text.Right)).WithTriviaFrom(visitedText.Right)));
            }
            string method=pure?"Caption":"DataText";
            if(IsType(type,"System.Windows.Forms.ToolStripItem"))method+="Item";
            Edits++;return SyntaxFactory.ParseExpression(LM+method+"("+visited.WithoutTrivia().ToFullString()+")").WithTriviaFrom(node);
        }
    }
}
