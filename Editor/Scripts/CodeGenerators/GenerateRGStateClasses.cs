using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RegressionGames.Editor.CodeGenerators
{
    // Dev Note: Not perfect, but mega time saver for generating this gook: https://roslynquoter.azurewebsites.net/
    public static class GenerateRGStateClasses
    {
        public static void Generate(List<RGStateAttributesInfo> rgStateAttributesInfos)
        {
            foreach (var rgStateAttributeInfo in rgStateAttributesInfos)
            {
                HashSet<string> usings = new()
                {
                    "System",
                    "System.Collections.Generic",
                    "RegressionGames",
                    "RegressionGames.RGBotConfigs",
                    "RegressionGames.StateActionTypes",
                    "UnityEngine"
                };

                if (!string.IsNullOrEmpty(rgStateAttributeInfo.NameSpace))
                {
                    usings.Add(rgStateAttributeInfo.NameSpace);
                }
                
                var className = $"RGState_{rgStateAttributeInfo.ClassName}";
                var componentType = rgStateAttributeInfo.ClassName;

                // Create a new compilation unit
                var compilationUnit = CompilationUnit()
                    .AddUsings(
                        usings.Select(v=>UsingDirective(ParseName(v))).ToArray()
                    );
                
                // Create a new class declaration with the desired name
                var classDeclaration = ClassDeclaration(className)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddBaseListTypes(SimpleBaseType(ParseTypeName("RGState")));

                // Create the private field
                var fieldDeclaration = FieldDeclaration(
                        VariableDeclaration(ParseTypeName(componentType))
                            .AddVariables(VariableDeclarator("myComponent")))
                    .AddModifiers(Token(SyntaxKind.PrivateKeyword));

                // Create the Start method
                var startMethod = GenerateStartMethod(componentType, rgStateAttributeInfo.State);

                // Create the GetState method
                var getStateMethod = GenerateGetStateMethod(componentType, rgStateAttributeInfo.State);

                // Add the members to the class declaration
                classDeclaration = classDeclaration
                    .AddMembers(fieldDeclaration, startMethod, getStateMethod);

                // Create namespace
                var namespaceDeclaration = NamespaceDeclaration(ParseName("RegressionGames.RGBotConfigs"))
                    .AddMembers(
                        // make sure to define the RGStateEntity class first in the file
                        // If you don't, then when the real class gets too large.. Roslyn will lose its way and forget that the RGStateEntity in the same file has a namespace... yes, really!
                        ClassDeclaration($"RGStateEntity_{rgStateAttributeInfo.ClassName}")
                            .AddModifiers(
                                Token(SyntaxKind.PublicKeyword)
                                // Only add one of the "class" keywords here
                            )
                            .AddBaseListTypes(
                                SimpleBaseType(
                                    GenericName(
                                            Identifier("RGStateEntity")
                                        )
                                        .WithTypeArgumentList(
                                            TypeArgumentList(
                                                SingletonSeparatedList<TypeSyntax>(
                                                    IdentifierName($"RGState_{rgStateAttributeInfo.ClassName}")
                                                )
                                            )
                                        )
                                )
                            ).AddMembers(
                                GenerateStateEntityFields(rgStateAttributeInfo.State)
                            ),
                        classDeclaration

                    );
                
                // Add the namespace declaration to the compilation unit
                compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);

                // Get the full code text
                var formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

                // Write the code to a .cs file
                // Save to 'Assets/RegressionGames/Runtime/GeneratedScripts/RGStates/{name}.cs'
                string subfolderName = Path.Combine("RegressionGames", "Runtime", "GeneratedScripts", "RGStates");
                string fileName = $"{className}.cs";
                string filePath = Path.Combine(Application.dataPath, subfolderName, fileName);
                string fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, fileContents);
                RGDebug.Log($"Successfully Generated {filePath}");
                AssetDatabase.Refresh();
            }
        }

        private static MethodDeclarationSyntax GenerateStartMethod(string componentType, List<RGStateAttributeInfo> stateInfos)
        {
            var startMethod = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Start")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithBody(Block());

            // Check for component
            startMethod = startMethod.AddBodyStatements(
                ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("myComponent"),
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                GenericName("GetComponent")
                                    .WithTypeArgumentList(
                                        TypeArgumentList(
                                            SingletonSeparatedList<TypeSyntax>(
                                                IdentifierName(componentType)
                                            )
                                        )
                                    )
                            )
                        )
                    )
                )
            );

            // Add null check statement
            startMethod = startMethod.AddBodyStatements(
                IfStatement(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName("myComponent"),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)
                    ),
                    Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                        IdentifierName("RGDebug.LogError")
                                    )
                                    .WithArgumentList(
                                        ArgumentList(
                                            SingletonSeparatedList(
                                                Argument(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal($"{componentType} not found")
                                                    )
                                                )
                                            )
                                        )
                                    )
                            )
                        )
                    )
                )
            );

            return startMethod;
        }

        private static MethodDeclarationSyntax GenerateGetStateMethod(string componentType, List<RGStateAttributeInfo> memberInfos)
        {
            var statements = new List<StatementSyntax>
            {
                // Create a new dictionary
                LocalDeclarationStatement(
                    VariableDeclaration(ParseTypeName("var"))
                        .AddVariables(VariableDeclarator("state")
                            .WithInitializer(EqualsValueClause(
                                ObjectCreationExpression(ParseTypeName("Dictionary<string, object>"))
                                    .WithArgumentList(ArgumentList())))))
            };

            // Add statements to add each state variable to the dictionary
            statements.AddRange(memberInfos.Select(mi =>
            {
                ExpressionSyntax valueExpression = mi.FieldType == "method" 
                    ? InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("myComponent"),
                            IdentifierName(mi.FieldName)
                        )
                    )
                    : MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("myComponent"),
                        IdentifierName(mi.FieldName)
                    );

                return ExpressionStatement(
                    InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("state"),
                                IdentifierName("Add")
                            )
                        )
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList(
                                    new[]
                                    {
                                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(mi.StateName))),
                                        Argument(valueExpression)
                                    }
                                )
                            )
                        )
                );
            }));

            // Add statement to return the dictionary
            statements.Add(ReturnStatement(IdentifierName("state")));

            // Create the GetState method
            var getStateMethod = MethodDeclaration(ParseTypeName("Dictionary<string, object>"), "GetState")
                .AddModifiers(Token(SyntaxKind.ProtectedKeyword),
                    Token(SyntaxKind.OverrideKeyword))
                .WithBody(Block(statements));

            return getStateMethod;
        }
        
        
        
        private static MemberDeclarationSyntax[] GenerateStateEntityFields(List<RGStateAttributeInfo> memberInfos)
        {
            var fields = new List<MemberDeclarationSyntax>();
            foreach (var memberInfo in memberInfos)
            {
                fields.Add(GeneratePropertyDeclaration(memberInfo));
            }
            
            return fields.ToArray();
        }

        private static PropertyDeclarationSyntax GeneratePropertyDeclaration(RGStateAttributeInfo memberInfo)
        {
            var specialNumberTypes = new [] {
                "float","double","decimal","sbyte","byte","short","ushort","int","uint","long","ulong"
            };
            if (specialNumberTypes.Contains(memberInfo.Type.ToLowerInvariant()))
            {
                return GeneratePropertyDeclarationForNumbers(memberInfo);
            } 
            // else generate regular format
            return GeneratePropertyDeclarationForNormalTypes(memberInfo);
        }

        private static PropertyDeclarationSyntax GeneratePropertyDeclarationForNormalTypes(RGStateAttributeInfo memberInfo)
        {
            return PropertyDeclaration(
                    IdentifierName(memberInfo.Type),
                    Identifier(memberInfo.StateName)
                )
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)
                    )
                )
                .WithExpressionBody(
                    ArrowExpressionClause(
                        CastExpression(
                            IdentifierName(memberInfo.Type),
                            InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ThisExpression(),
                                        IdentifierName("GetValueOrDefault")
                                    )
                                )
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression,
                                                    Literal(memberInfo.StateName)
                                                )
                                            )
                                        )
                                    )
                                )
                        )
                    )
                )
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken)
                );
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
        private static PropertyDeclarationSyntax GeneratePropertyDeclarationForNumbers(RGStateAttributeInfo memberInfo)
        {
            return PropertyDeclaration(
                    IdentifierName(memberInfo.Type),
                    Identifier(memberInfo.StateName)
                )
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)
                    )
                )
                .WithExpressionBody(
                    ArrowExpressionClause(
                        CastExpression(
                            IdentifierName(memberInfo.Type),
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(memberInfo.Type),
                                    IdentifierName("Parse")
                                )
                            )
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList<ArgumentSyntax>(
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
                                                            SingletonSeparatedList<ArgumentSyntax>(
                                                                Argument(
                                                                    LiteralExpression(
                                                                        SyntaxKind.StringLiteralExpression,
                                                                        Literal(memberInfo.StateName)
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
}