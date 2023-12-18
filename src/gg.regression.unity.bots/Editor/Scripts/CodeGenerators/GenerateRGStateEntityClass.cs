using System.Collections.Generic;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
#endif

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace RegressionGames.Editor.CodeGenerators
{
#if UNITY_EDITOR
    // Dev Note: Not perfect, but mega time saver for generating this gook: https://roslynquoter.azurewebsites.net/
    public static class GenerateRGStateEntityClass
    {
        public static Task Generate(
            string filePath,
            string entityTypeName,
            bool isPlayer,
            string behaviourName,
            string behaviourNamespace,
            List<RGCodeGenerator.StateBehaviourPropertyInfo> attributeInfos)
        {
            List<string> usingList = new()
            {
                "System",
                "RegressionGames",
                "RegressionGames.StateActionTypes",
                "UnityEngine"
            };

            var className = $"RGStateEntity_{behaviourName}";

            // Create a new compilation unit
            var compilationUnit = CompilationUnit()
                .AddUsings(
                    usingList.Select(v => UsingDirective(ParseName(v))).ToArray()
                );

            // Create a new class declaration with the desired name
            var classDeclaration = ClassDeclaration(className)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SimpleBaseType(ParseTypeName("RGStateEntity")));

            // Create namespace
            var namespaceDeclaration = NamespaceDeclaration(ParseName(behaviourNamespace))
                .AddMembers(classDeclaration);
            
            
            //Create the static methods
            classDeclaration.AddMembers(
                GenerateBehaviourTypeField(behaviourName),
                GenerateEntityTypeNameMethod(entityTypeName),
                GenerateBehaviourTypeMethod(),
                GenerateIsPlayerMethod(isPlayer)
            );
            
            //Create the populate method
            classDeclaration.AddMembers(
                GeneratePopulateMethod(behaviourName, attributeInfos)
            );
            
            //Generate the property accessors
            var properties = attributeInfos.Select(a => GeneratePropertyDeclaration(a.Type, a.StateName)).ToArray();
            classDeclaration.AddMembers(properties);
            
            // Add it all to the compilation unit
            compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);
            
            // Get the full code text
            var formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

            // Write the code to a .cs file
            var fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;
            return File.WriteAllTextAsync(filePath, fileContents);
        }

        private static MethodDeclarationSyntax GeneratePopulateMethod(string behaviourName, List<RGCodeGenerator.StateBehaviourPropertyInfo> attributeInfos)
        {
            var body = new SyntaxList<StatementSyntax>
            {
                LocalDeclarationStatement(
                    VariableDeclaration(
                            IdentifierName(
                                Identifier(
                                    TriviaList(),
                                    SyntaxKind.VarKeyword,
                                    "var",
                                    "var",
                                    TriviaList())))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("behaviour"))
                                    .WithInitializer(
                                        EqualsValueClause(
                                            CastExpression(
                                                IdentifierName(behaviourName),
                                                IdentifierName("monoBehaviour")))))))
            };
            

            foreach (var attributeInfo in attributeInfos)
            {
                body.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        ElementAccessExpression(
                                ThisExpression())
                            .WithArgumentList(
                                BracketedArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(attributeInfo.StateName)))))),
                        attributeInfo.IsMethod ?
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("behaviour"),
                                IdentifierName(attributeInfo.FieldName)
                                )
                            )
                            : MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("behaviour"),
                                IdentifierName(attributeInfo.FieldName)
                                )
                        )
                    )
                );
            }

            var method = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier("PopulateFromMonoBehaviour"))
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.OverrideKeyword)}))
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(
                                    Identifier("monoBehaviour"))
                                .WithType(
                                    IdentifierName("MonoBehaviour")))))
                .WithBody(
                    Block(body));

            return method;
        }

        private static FieldDeclarationSyntax GenerateBehaviourTypeField(string behaviourName)
        {
            return FieldDeclaration(
                    VariableDeclaration(
                            IdentifierName("Type"))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("BehaviourType"))
                                    .WithInitializer(
                                        EqualsValueClause(
                                            TypeOfExpression(
                                                IdentifierName(behaviourName)))))))
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PrivateKeyword),
                            Token(SyntaxKind.StaticKeyword),
                            Token(SyntaxKind.ReadOnlyKeyword)}));
        }

        private static MethodDeclarationSyntax GenerateEntityTypeNameMethod(string entityTypeName)
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.StringKeyword)),
                    Identifier("GetEntityTypeName"))
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.NewKeyword),
                            Token(SyntaxKind.StaticKeyword)}))
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(entityTypeName))))));
        }
        
        private static MethodDeclarationSyntax GenerateBehaviourTypeMethod()
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.TypeKeyword)),
                    Identifier("GetBehaviourType"))
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.NewKeyword),
                            Token(SyntaxKind.StaticKeyword)}))
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                IdentifierName("BehaviourType")))));
        }

        private static MethodDeclarationSyntax GenerateIsPlayerMethod(bool isPlayer)
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.BoolKeyword)),
                    Identifier("GetIsPlayer"))
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.NewKeyword),
                            Token(SyntaxKind.StaticKeyword)}))
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                IdentifierName($"{isPlayer}")))));
        }

        private static MemberDeclarationSyntax GeneratePropertyDeclaration(string propertyType, string propertyName)
        {
            var specialNumberTypes = new [] {
                "float","double","decimal","sbyte","byte","short","ushort","int","uint","long","ulong"
            };
            if (specialNumberTypes.Contains(propertyType.ToLowerInvariant()))
            {
                return GeneratePropertyDeclarationForNumbers(propertyType, propertyName);
            }
            // else generate regular format
            return GeneratePropertyDeclarationForNormalTypes(propertyType, propertyName);
        }

        private static PropertyDeclarationSyntax GeneratePropertyDeclarationForNormalTypes(string propertyType, string propertyName)
        {
            var myName = propertyName.Replace(" ", "_");
            return PropertyDeclaration(
                    IdentifierName(propertyType),
                        Identifier(myName))
                    .WithModifiers(
                        TokenList(
                            Token(SyntaxKind.PublicKeyword)))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            CastExpression(
                                IdentifierName(propertyType),
                                ElementAccessExpression(
                                        ThisExpression())
                                    .WithArgumentList(
                                        BracketedArgumentList(
                                            SingletonSeparatedList(
                                                Argument(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal(propertyName)))))))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken));
        }
        /**
         * <summary>Handles type conversions between long/int , double/float,etc  seamlessly regardless of what type the
         * json deserializer picked for the dictionary when handling remote bot action requests from javascript</summary>
         *
         * Example outputs for each primitive type...
         * public float f1 => (float)float.Parse(this.GetValueOrDefault("f1").ToString());
         * public double dd1 => (double)double.Parse(this.GetValueOrDefault("dd1").ToString());
         * public decimal d1 => (decimal)decimal.Parse(this.GetValueOrDefault("d1").ToString());
         * public sbyte sb1 => (sbyte)sbyte.Parse(this.GetValueOrDefault("sb1").ToString());
         * public byte b1 => (byte)byte.Parse(this.GetValueOrDefault("b1").ToString());
         * public short s1 => (short)short.Parse(this.GetValueOrDefault("s1").ToString());
         * public ushort us1 => (ushort)ushort.Parse(this.GetValueOrDefault("us1").ToString());
         * public int i1 => (int)int.Parse(this.GetValueOrDefault("i1").ToString());
         * public uint ui1 => (uint)uint.Parse(this.GetValueOrDefault("ui1").ToString());
         * public long l1 => (long)long.Parse(this.GetValueOrDefault("l1").ToString());
         * public ulong ul1 => (ulong)ulong.Parse(this.GetValueOrDefault("ul1").ToString());
         * public nint ni1 => (nint)this.GetValueOrDefault("ni1");
         * public nuint nui1 => (nuint)this.GetValueOrDefault("nui1");
         * public bool isAlive => (bool)this.GetValueOrDefault("isAlive");
         */
        private static PropertyDeclarationSyntax GeneratePropertyDeclarationForNumbers(string propertyType, string propertyName)
        {
            return PropertyDeclaration(
                    IdentifierName(propertyType),
                    Identifier(propertyName.Replace(" ", "_"))
                )
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)
                    )
                )
                .WithExpressionBody(
                    ArrowExpressionClause(
                        CastExpression(
                            IdentifierName(propertyType),
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(propertyType),
                                    IdentifierName("Parse")
                                )
                            )
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            ThisExpression(),
                                                            IdentifierName("GetValueOrDefault")
                                                        )
                                                    )
                                                    .WithArgumentList(
                                                        ArgumentList(
                                                            SingletonSeparatedList(
                                                                Argument(
                                                                    LiteralExpression(
                                                                        SyntaxKind.StringLiteralExpression,
                                                                        Literal(propertyName)
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    ),
                                                    IdentifierName("ToString")
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                ).WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken)
                );
        }

    }
#endif
}
