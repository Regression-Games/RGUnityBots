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
    // Dev Note: Not perfect, but mega time saver for generating this gobbledygook: https://roslynquoter.azurewebsites.net/
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
                .AddBaseListTypes(SimpleBaseType(ParseTypeName("RGStateEntityBase")))
                .AddMembers(
                    GenerateBehaviourType(behaviourName),
                    GenerateEntityType(entityTypeName),
                    GenerateIsPlayer(isPlayer),
                    GenerateEntityTypeMethod(),
                    GenerateIsPlayerMethod(),
                    GeneratePopulateMethod(behaviourName, attributeInfos)
                );

            var properties = attributeInfos.Where(a => a !=null).Select(a => GeneratePropertyDeclaration(a.Type, a.StateName)).Where(v => v != null).ToArray();
            if (properties.Length > 0)
            {
                classDeclaration = classDeclaration.AddMembers(
                    properties
                );
            }
            
            
            var compilationUnitMembers = new List<MemberDeclarationSyntax>();
            // Create namespace
            if (string.IsNullOrEmpty(behaviourNamespace))
            {
                compilationUnitMembers.Add(classDeclaration);
            }
            else
            {
                compilationUnitMembers.Add(
                    NamespaceDeclaration(ParseName(behaviourNamespace))
                    .AddMembers(classDeclaration));
            }

            // Add it all to the compilation unit
            compilationUnit = compilationUnit.AddMembers(compilationUnitMembers.ToArray());
            
            // Get the full code text
            var formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

            // Write the code to a .cs file
            var fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;
            return File.WriteAllTextAsync(filePath, fileContents);
        }

        private static MethodDeclarationSyntax GeneratePopulateMethod(string behaviourName, List<RGCodeGenerator.StateBehaviourPropertyInfo> attributeInfos)
        {
            var body = new List<StatementSyntax>
            {
                LocalDeclarationStatement(
                    VariableDeclaration(
                            IdentifierName(
                                Identifier(
                                    TriviaList(),
                                    SyntaxKind.VarKeyword,
                                    "var",
                                    "var",
                                    TriviaList()
                                )
                            )
                        )
                        .WithVariables(
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(
                                        Identifier("behaviour")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            CastExpression(
                                                IdentifierName(behaviourName),
                                                IdentifierName("monoBehaviour")
                                            )
                                        )
                                    )
                            )
                        )
                )
            };

            if (attributeInfos != null)
            {
                foreach (var attributeInfo in attributeInfos)
                {
                    if (attributeInfo != null)
                    {
                        body.Add(
                            ExpressionStatement(
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
                                    attributeInfo.IsMethod
                                        ? InvocationExpression(
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
                }
            }

            var method = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)
                    ),
                    Identifier("PopulateFromMonoBehaviour")
                )
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.OverrideKeyword)
                        }
                    )
                )
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList<ParameterSyntax>(
                            Parameter(
                                    Identifier("monoBehaviour")
                                )
                                .WithType(
                                    IdentifierName("MonoBehaviour")
                                )
                        )
                    )
                )
                .WithBody(
                    Block(body));

            return method;
        }

        private static MethodDeclarationSyntax GenerateEntityTypeMethod()
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.StringKeyword)
                    ),
                    Identifier("GetEntityType")
                )
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.OverrideKeyword)
                        }
                    )
                )
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                IdentifierName("EntityTypeName")
                            )
                        )
                    )
                );
        }
        
        private static MethodDeclarationSyntax GenerateIsPlayerMethod()
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.BoolKeyword)
                    ),
                    Identifier("GetIsPlayer")
                )
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.OverrideKeyword)
                        }
                    )
                )
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                IdentifierName("IsPlayer")
                            )
                        )
                    )
                );
        }

        private static FieldDeclarationSyntax GenerateEntityType(string entityTypeName)
        {
            return FieldDeclaration(
                    VariableDeclaration(
                            PredefinedType(
                                Token(SyntaxKind.StringKeyword)
                            )
                        )
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("EntityTypeName")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(entityTypeName)
                                            )
                                        )
                                    )
                            )
                        )
                )
                .WithModifiers(
                    TokenList(
                        new[]
                        {
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.StaticKeyword),
                            Token(SyntaxKind.ReadOnlyKeyword)
                        }
                    )
                );
        }

        private static FieldDeclarationSyntax GenerateBehaviourType(string behaviourName)
        {
            return FieldDeclaration(
                    VariableDeclaration(
                            IdentifierName("Type")
                        )
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("BehaviourType")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            TypeOfExpression(
                                                IdentifierName(behaviourName)
                                            )
                                        )
                                    )
                            )
                        )
                )
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.StaticKeyword),
                            Token(SyntaxKind.ReadOnlyKeyword)
                        }
                    )
                );
        }

        private static FieldDeclarationSyntax GenerateIsPlayer(bool isPlayer)
        {
            return FieldDeclaration(
                    VariableDeclaration(
                            PredefinedType(
                                Token(SyntaxKind.BoolKeyword)
                            )
                        )
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("IsPlayer")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            LiteralExpression(
                                                isPlayer ?
                                                SyntaxKind.TrueLiteralExpression :
                                                SyntaxKind.FalseLiteralExpression
                                            )
                                        )
                                    )
                            )
                        )
                )
                .WithModifiers(
                    TokenList(
                        new []{
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.StaticKeyword),
                            Token(SyntaxKind.ReadOnlyKeyword)
                        }
                    )
                );
        }

        private static PropertyDeclarationSyntax GeneratePropertyDeclaration(string propertyType, string propertyName)
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
         * public float f1 => (float)float.Parse(this.GetField("f1").ToString());
         * public double dd1 => (double)double.Parse(this.GetField("dd1").ToString());
         * public decimal d1 => (decimal)decimal.Parse(this.GetField("d1").ToString());
         * public sbyte sb1 => (sbyte)sbyte.Parse(this.GetField("sb1").ToString());
         * public byte b1 => (byte)byte.Parse(this.GetField("b1").ToString());
         * public short s1 => (short)short.Parse(this.GetField("s1").ToString());
         * public ushort us1 => (ushort)ushort.Parse(this.GetField("us1").ToString());
         * public int i1 => (int)int.Parse(this.GetField("i1").ToString());
         * public uint ui1 => (uint)uint.Parse(this.GetField("ui1").ToString());
         * public long l1 => (long)long.Parse(this.GetField("l1").ToString());
         * public ulong ul1 => (ulong)ulong.Parse(this.GetField("ul1").ToString());
         * public nint ni1 => (nint)this.GetField("ni1");
         * public nuint nui1 => (nuint)this.GetField("nui1");
         * public bool isAlive => (bool)this.GetField("isAlive");
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
                                                            IdentifierName("GetField")
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
