using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
#endif

namespace RegressionGames.Editor.CodeGenerators
{
#if UNITY_EDITOR
    // Dev Note: Not perfect, but mega time saver for generating this gobbledygook: https://roslynquoter.azurewebsites.net/
    public static class GenerateRGActionsClass
    {
        public static Task Generate(
            string filePath,
            string behaviourName,
            string behaviourNamespace,
            List<RGActionAttributeInfo> botActions)
        {

            HashSet<string> usingSet = new()
            {
                "System",
                "System.Collections.Generic",
                "System.Collections.ObjectModel",
                "Newtonsoft.Json",
                "RegressionGames",
                "RegressionGames.StateActionTypes",
                "UnityEngine"
            };

            var className = $"RGActions_{behaviourName}";

            var entityTypeName = behaviourName;
            
            List<SyntaxNodeOrToken> delegateList = new();
            
            List<MemberDeclarationSyntax> actionClassDeclarations = new();
            // process each action
            foreach (var botAction in botActions)
            {
                if (!botAction.ShouldGenerateCSFile)
                {
                    continue;
                }
                
                entityTypeName = botAction.EntityTypeName ?? entityTypeName;
                
                // Add RGActionRequest class
                actionClassDeclarations.Add(
                    ClassDeclaration(
                            $"RGActionRequest_{botAction.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}")
                        .AddModifiers(
                            Token(SyntaxKind.PublicKeyword)
                            // Only add one of the "class" keywords here
                        )
                        .AddBaseListTypes(
                            SimpleBaseType(ParseTypeName("RGActionRequest"))
                        ).AddMembers(
                            GenerateActionRequestConstructor(botAction),
                            GenerateEntityTypeName(entityTypeName),
                            GenerateActionRequestActionName(botAction)
                        ).AddMembers(
                            GenerateActionRequestFields(botAction).ToArray()
                        )
                    );
                
                // Add RGAction class
                actionClassDeclarations.Add(
                    ClassDeclaration(
                            $"RGAction_{botAction.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}")
                        .AddModifiers(
                            Token(SyntaxKind.PublicKeyword)
                        ).AddBaseListTypes(
                            SimpleBaseType(ParseTypeName("IRGAction"))
                        ).AddMembers(
                            GenerateInvokeOnGameObjectWithActionRequest(botAction),
                            GenerateInvokeOnGameObjectWithInput(botAction),
                            GenerateInvokeOnGameObjectWithArgs(botAction)
                        )
                );

                //Add the stuff to populate main class delegate list
                delegateList.Add(
                    InitializerExpression(
                        SyntaxKind.ComplexElementInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            new SyntaxNodeOrToken[]{
                                MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName($"RGActionRequest_{botAction.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}"),
                                    IdentifierName("ActionName")),
                                Token(SyntaxKind.CommaToken),
                                ObjectCreationExpression(
                                    GenericName(
                                        Identifier("Action"))
                                    .WithTypeArgumentList(
                                        TypeArgumentList(
                                            SeparatedList<TypeSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            IdentifierName("GameObject"),
                                            Token(SyntaxKind.CommaToken),
                                            IdentifierName("RGActionRequest")})
                                            )
                                        )
                                    )
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList(
                                            Argument(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName($"RGAction_{botAction.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}"),
                                                    IdentifierName("InvokeOnGameObject"))
                                                )
                                            )
                                        )
                                    )
                                
                            })
                        )
                    );
                delegateList.Add(Token(SyntaxKind.CommaToken));
            }
            
            var mainClassDeclaration = ClassDeclaration(className)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SimpleBaseType(ParseTypeName("IRGActions")))
                // BehaviourTypeField
                .AddMembers(                            
                    GenerateBehaviourType(behaviourName), 
                    GenerateEntityTypeName(entityTypeName),
                    GenerateMainClassDelegateDictionary(delegateList)
                );

            var serializationClassDeclaration = GenerateSerializationClass(behaviourName, botActions);

            var compilationUnitMembers = new List<MemberDeclarationSyntax>();
            // Create namespace
            if (string.IsNullOrEmpty(behaviourNamespace))
            {
                compilationUnitMembers.Add(mainClassDeclaration);
                compilationUnitMembers.AddRange(actionClassDeclarations);
                compilationUnitMembers.Add(serializationClassDeclaration);
            }
            else
            {
                compilationUnitMembers.Add(NamespaceDeclaration(ParseName(behaviourNamespace))
                    .AddMembers(mainClassDeclaration)
                    .AddMembers(actionClassDeclarations.ToArray())
                    .AddMembers(serializationClassDeclaration));
            }
            
            // Create a new compilation unit
            var compilationUnit = CompilationUnit()
                .AddUsings(
                    usingSet.Select(v => UsingDirective(ParseName(v))).ToArray()
                )
                .AddMembers(compilationUnitMembers.ToArray());

            // Format the generated code
            var formattedCode = compilationUnit.NormalizeWhitespace(eol: Environment.NewLine).ToFullString();
            
            var fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;

            return File.WriteAllTextAsync(filePath, fileContents);
        }

        private static MemberDeclarationSyntax GenerateMainClassDelegateDictionary(List<SyntaxNodeOrToken> delegateList)
        {
            return FieldDeclaration(
                VariableDeclaration(
                        GenericName(
                                Identifier("IDictionary"))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SeparatedList<TypeSyntax>(
                                        new SyntaxNodeOrToken[]
                                        {
                                            PredefinedType(
                                                Token(SyntaxKind.StringKeyword)),
                                            Token(SyntaxKind.CommaToken),
                                            IdentifierName("Delegate")
                                        }))))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(
                                    Identifier("ActionRequestDelegates"))
                                .WithInitializer(
                                    EqualsValueClause(
                                        ObjectCreationExpression(
                                                GenericName(
                                                        Identifier("ReadOnlyDictionary"))
                                                    .WithTypeArgumentList(
                                                        TypeArgumentList(
                                                            SeparatedList<TypeSyntax>(
                                                                new SyntaxNodeOrToken[]
                                                                {
                                                                    PredefinedType(
                                                                        Token(SyntaxKind.StringKeyword)),
                                                                    Token(SyntaxKind.CommaToken),
                                                                    IdentifierName("Delegate")
                                                                })
                                                        )
                                                    )
                                            )
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SingletonSeparatedList(
                                                        Argument(
                                                            ObjectCreationExpression(
                                                                    GenericName(
                                                                            Identifier("Dictionary"))
                                                                        .WithTypeArgumentList(
                                                                            TypeArgumentList(
                                                                                SeparatedList<TypeSyntax>(
                                                                                    new SyntaxNodeOrToken[]
                                                                                    {
                                                                                        PredefinedType(
                                                                                            Token(SyntaxKind
                                                                                                .StringKeyword)),
                                                                                        Token(SyntaxKind.CommaToken),
                                                                                        IdentifierName("Delegate")
                                                                                    })
                                                                            )
                                                                        )
                                                                )
                                                                .WithArgumentList(
                                                                    ArgumentList())
                                                                .WithInitializer(
                                                                    InitializerExpression(
                                                                        SyntaxKind.CollectionInitializerExpression,
                                                                        SeparatedList<ExpressionSyntax>(
                                                                            delegateList.ToArray()
                                                                        )
                                                                    )
                                                                )
                                                        )
                                                    )
                                                )
                                            )
                                    )
                                )
                        )
                    )
            ).WithModifiers(
                TokenList(
                    new[]
                    {
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword),
                        Token(SyntaxKind.ReadOnlyKeyword)
                    })
            );
        }

        private static MemberDeclarationSyntax GenerateInvokeOnGameObjectWithActionRequest(RGActionAttributeInfo action)
        {
            var argumentList = new List<SyntaxNodeOrToken>()
            {
                Argument(
                    IdentifierName("gameObject")
                ),
                Token(SyntaxKind.CommaToken)
            };

            for (var index = 0; index < action.Parameters.Count; index++)
            {
                var rgParameterInfo = action.Parameters[index];
                argumentList.Add(Argument(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("myActionRequest"),
                            IdentifierName(rgParameterInfo.Name)
                        )
                    )
                );
                if (index + 1 < action.Parameters.Count)
                {
                    argumentList.Add(Token(SyntaxKind.CommaToken));
                }
            }

            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)
                    ),
                    Identifier("InvokeOnGameObject")
                )
                .WithModifiers(
                    TokenList(
                        new[]
                        {
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.StaticKeyword)
                        }
                    )
                )
                .WithParameterList(
                    ParameterList(
                        SeparatedList<ParameterSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                Parameter(
                                        Identifier("gameObject")
                                    )
                                    .WithType(
                                        IdentifierName("GameObject")
                                    ),
                                Token(SyntaxKind.CommaToken),
                                Parameter(
                                        Identifier("actionRequest")
                                    )
                                    .WithType(
                                        IdentifierName("RGActionRequest")
                                    )
                            }
                        )
                    )
                )
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            IfStatement(
                                    IsPatternExpression(
                                        IdentifierName("actionRequest"),
                                        DeclarationPattern(
                                            IdentifierName(
                                                $"RGActionRequest_{action.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(action.ActionName)}"),
                                            SingleVariableDesignation(
                                                Identifier("myActionRequest")
                                            )
                                        )
                                    ),
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        IdentifierName("InvokeOnGameObject")
                                                    )
                                                    .WithArgumentList(
                                                        ArgumentList(
                                                            SeparatedList<ArgumentSyntax>(argumentList.ToArray())
                                                        )
                                                    )
                                            )
                                        )
                                    )
                                )
                                .WithIfKeyword(
                                    Token(
                                        TriviaList(
                                            Comment(
                                                "// optimize this for local C# bots to avoid all the conversions/etc")
                                        ),
                                        SyntaxKind.IfKeyword,
                                        TriviaList()
                                    )
                                )
                                .WithElse(
                                    ElseClause(
                                        Block(
                                            SingletonList<StatementSyntax>(
                                                ExpressionStatement(
                                                    InvocationExpression(
                                                            IdentifierName("InvokeOnGameObject")
                                                        )
                                                        .WithArgumentList(
                                                            ArgumentList(
                                                                SeparatedList<ArgumentSyntax>(
                                                                    new SyntaxNodeOrToken[]
                                                                    {
                                                                        Argument(
                                                                            IdentifierName("gameObject")
                                                                        ),
                                                                        Token(SyntaxKind.CommaToken),
                                                                        Argument(
                                                                            MemberAccessExpression(
                                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                                IdentifierName("actionRequest"),
                                                                                IdentifierName("Input")
                                                                            )
                                                                        )
                                                                    }
                                                                )
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
        }

        private static MemberDeclarationSyntax GenerateInvokeOnGameObjectWithInput(RGActionAttributeInfo action)
        {
            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)
                    ),
                    Identifier("InvokeOnGameObject")
                )
                .WithModifiers(
                    TokenList(
                        new[]
                        {
                            Token(SyntaxKind.PrivateKeyword),
                            Token(SyntaxKind.StaticKeyword)
                        }
                    )
                )
                .WithParameterList(
                    ParameterList(
                        SeparatedList<ParameterSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                Parameter(
                                        Identifier("gameObject")
                                    )
                                    .WithType(
                                        IdentifierName("GameObject")
                                    ),
                                Token(SyntaxKind.CommaToken),
                                Parameter(
                                        Identifier("input")
                                    )
                                    .WithType(
                                        GenericName(
                                                Identifier("Dictionary")
                                            )
                                            .WithTypeArgumentList(
                                                TypeArgumentList(
                                                    SeparatedList<TypeSyntax>(
                                                        new SyntaxNodeOrToken[]
                                                        {
                                                            PredefinedType(
                                                                Token(SyntaxKind.StringKeyword)
                                                            ),
                                                            Token(SyntaxKind.CommaToken),
                                                            PredefinedType(
                                                                Token(SyntaxKind.ObjectKeyword)
                                                            )
                                                        }
                                                    )
                                                )
                                            )
                                    )
                            }
                        )
                    )
                )
                .WithBody(
                    GenerateWithInputMethodBody(action)
                );
        }

        private static MemberDeclarationSyntax GenerateInvokeOnGameObjectWithArgs(RGActionAttributeInfo action)
        {
            var methodArguments = new List<SyntaxNodeOrToken>();

            for (var index = 0; index < action.Parameters.Count; index++)
            {
                var rgParameterInfo = action.Parameters[index];
                methodArguments.Add(
                    Argument(
                        CastExpression(
                            IdentifierName(rgParameterInfo.Type),
                            ElementAccessExpression(
                                    IdentifierName("args"))
                                .WithArgumentList(
                                    BracketedArgumentList(
                                        SingletonSeparatedList(
                                            Argument(
                                                LiteralExpression(
                                                    SyntaxKind.NumericLiteralExpression,
                                                    Literal(index))))))))
                );
                if (index+1 < action.Parameters.Count)
                methodArguments.Add(Token(SyntaxKind.CommaToken));
            }

            return MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier("InvokeOnGameObject"))
                .WithModifiers(
                    TokenList(
                        new[]
                        {
                            Token(SyntaxKind.PrivateKeyword),
                            Token(SyntaxKind.StaticKeyword)
                        }))
                .WithParameterList(
                    ParameterList(
                        SeparatedList<ParameterSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                Parameter(
                                        Identifier("gameObject"))
                                    .WithType(
                                        IdentifierName("GameObject")),
                                Token(SyntaxKind.CommaToken),
                                Parameter(
                                        Identifier("args"))
                                    .WithModifiers(
                                        TokenList(
                                            Token(SyntaxKind.ParamsKeyword)))
                                    .WithType(
                                        ArrayType(
                                                PredefinedType(
                                                    Token(SyntaxKind.ObjectKeyword)))
                                            .WithRankSpecifiers(
                                                SingletonList(
                                                    ArrayRankSpecifier(
                                                        SingletonSeparatedList<ExpressionSyntax>(
                                                            OmittedArraySizeExpression())))))
                            })))
                .WithBody(
                    Block(
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
                                                Identifier("monoBehaviour"))
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("gameObject"),
                                                            GenericName(
                                                                    Identifier("GetComponent"))
                                                                .WithTypeArgumentList(
                                                                    TypeArgumentList(
                                                                        SingletonSeparatedList<TypeSyntax>(
                                                                            IdentifierName(action.BehaviourName))))))))))),
                        IfStatement(
                            BinaryExpression(
                                SyntaxKind.EqualsExpression,
                                IdentifierName("monoBehaviour"),
                                LiteralExpression(
                                    SyntaxKind.NullLiteralExpression)),
                            Block(
                                ExpressionStatement(
                                    InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("RGDebug"),
                                                IdentifierName("LogError")))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SeparatedList<ArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]{
                                                    Argument(
                                                        InterpolatedStringExpression(
                                                                Token(SyntaxKind.InterpolatedStringStartToken))
                                                            .WithContents(
                                                                List(
                                                                    new InterpolatedStringContentSyntax[]
                                                                    {
                                                                        InterpolatedStringText()
                                                                            .WithTextToken(
                                                                                Token(
                                                                                    TriviaList(),
                                                                                    SyntaxKind
                                                                                        .InterpolatedStringTextToken,
                                                                                    $"Error: Regression Games internal error... Somehow RGAction: {action.ActionName} got registered on a GameObject where MonoBehaviour: {action.BehaviourName} does not exist.",
                                                                                    $"Error: Regression Games internal error... Somehow RGAction: {action.ActionName} got registered on a GameObject where MonoBehaviour: {action.BehaviourName} does not exist.",
                                                                                    TriviaList()))
                                                                    })
                                                            )
                                                    ),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        IdentifierName("gameObject"))
                                                    }
                                                )
                                            )
                                        )
                                ),
                                ReturnStatement())),
                        ExpressionStatement(
                            InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("monoBehaviour"),
                                        IdentifierName(action.MethodName)))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList<ArgumentSyntax>(
                                            methodArguments.ToArray()
                                        )
                                    )
                                )
                        )
                    )
                );
        }

        private static BlockSyntax GenerateWithInputMethodBody(RGActionAttributeInfo action)
        {
            var parameterParsingStatements = new List<StatementSyntax>();
            var methodInvocationArguments = new List<string>();

            foreach (var parameter in action.Parameters)
            {
                var paramName = parameter.Name;

                methodInvocationArguments.Add(paramName);
                parameterParsingStatements.Add(ParseStatement($"{parameter.Type} {paramName} = default;"));
                parameterParsingStatements.Add(IfStatement(IfCondition(parameter), IfBody(action.BehaviourName, parameter), ElseBody(parameter)));
            }

            var methodInvocationArgumentsString = methodInvocationArguments.Count > 0 ?
                                                     string.Join(", ", methodInvocationArguments) :
                                                     string.Empty;

            parameterParsingStatements.Add(ParseStatement($"InvokeOnGameObject(gameObject, {methodInvocationArgumentsString});"));

            return Block(parameterParsingStatements);
        }

        /**
         * input.TryGetValue("targetId", out var targetIdInput)
         */
        private static InvocationExpressionSyntax IfCondition(RGParameterInfo param)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("input"),
                    IdentifierName("TryGetValue")
                    )).WithArgumentList(
                ArgumentList(
                    SeparatedList(new List<ArgumentSyntax>
                    {
                        Argument(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(param.Name)
                            )
                        ),
                        Argument(
                            DeclarationExpression(
                                    IdentifierName("var"),
                                    SingleVariableDesignation(Identifier($"{param.Name}Input")))
                            ).WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
                    })
                ));
        }

        /**
         * try
         * {
         *      string:
         *      key = keyInput.ToString();
         *
         *      primitive:
         *      KeyType.TryParse(keyInput.ToString(), out key);
         *
         *      nonprimitive:
         *      key = RGSerialization_${behaviourName}.Deserialize_KeyType(key.ToString());
         *
         * }
         * catch (Exception ex)
         * {
         *      RGDebug.LogError("Failed to parse 'skillId'");
         *      RGDebug.LogError(ex.Message);
         * }
         */
        private static StatementSyntax IfBody(string behaviourName, RGParameterInfo param)
        {
            var paramType = param.Type;
            var paramName = param.Name;

            string tryParseStatement;
            if (paramType.ToLower() == "string" || paramType.ToLower() == "system.string")
            {
                tryParseStatement = $"{paramName} = {paramName}Input.ToString();";
            }
            else if (RGUtils.IsCSharpPrimitive(paramType))
            {
                tryParseStatement = $"{paramType}.TryParse({paramName}Input.ToString(), out {paramName});";
            }
            else
            {
                // Do direct type cast whenever possible, taking into account nullable types
                tryParseStatement = $"if ({paramName}Input is {paramType.Replace("?", "")}";
                if (param.Nullable)
                {
                    tryParseStatement += " or null";
                }
                tryParseStatement += ")";
                tryParseStatement += $"{{ {paramName} = ({paramType}){paramName}Input; }}";
                tryParseStatement += $"else {{ {paramName} = RGSerialization_{behaviourName}.Deserialize_{paramType.Replace(".", "_").Replace("?", "")}";

                if (param.Nullable)
                {
                    tryParseStatement += "_Nullable";
                }
                tryParseStatement += $"({paramName}Input.ToString());";

                tryParseStatement += "}";
            }

            var tryBlock = Block(SingletonList(
                ParseStatement(tryParseStatement)
            ));

            var catchBlock = CatchClause()
                .WithDeclaration(CatchDeclaration(ParseTypeName("Exception"), Identifier("ex")))
                .WithBlock(Block(new StatementSyntax[]
                {
                    ParseStatement($"RGDebug.LogError($\"Failed to parse '{paramName}' - {{ex}}\");"),
                }));

            return Block(
                TryStatement()
                    .WithBlock(tryBlock)
                    .WithCatches(SingletonList(catchBlock))
            );
        }

        /**
         * RGDebug.LogError("No parameter 'key' found");
         * return;
         */
        private static ElseClauseSyntax ElseBody(RGParameterInfo param)
        {
            if (param.Nullable)
            {
                return default(ElseClauseSyntax);
            }

            // Validation check for key existence if param must be non-null
            return ElseClause(Block(new StatementSyntax[]
                    {
                        ExpressionStatement(
                            InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("RGDebug"),
                                        IdentifierName("LogError")
                                )
                            ).WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal($"No parameter '{param.Name}' found")
                                                )
                                            )
                                        )
                                    )
                                )
                            ), ReturnStatement()
                    }
                ));
        }

        private static MemberDeclarationSyntax GenerateActionRequestConstructor(RGActionAttributeInfo action)
        {
            var methodParameters = new List<ParameterSyntax>();
            var parameterParsingStatements = new List<StatementSyntax>();
            foreach (var rgParameterInfo in action.Parameters)
            {
                methodParameters.Add(Parameter(Identifier(rgParameterInfo.Name))
                    .WithType(ParseTypeName(rgParameterInfo.Type)));
            
                parameterParsingStatements.Add(
                    ParseStatement($"Input[\"{rgParameterInfo.Name}\"] = {rgParameterInfo.Name};"));
            }
            
            var methodBody = Block(parameterParsingStatements);

            var constructor = ConstructorDeclaration(
                $"RGActionRequest_{action.BehaviourName}_{CodeGeneratorUtils.SanitizeActionName(action.ActionName)}"
            )
            .WithInitializer(
                ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(action.ActionName)))))))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(methodParameters.ToArray())
            .WithBody(methodBody);

            return constructor;
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
        
        private static MemberDeclarationSyntax GenerateEntityTypeName(string entitytTypeName)
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
                                                Literal(entitytTypeName)
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
        
        private static MemberDeclarationSyntax GenerateActionRequestActionName(RGActionAttributeInfo action)
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
                                        Identifier("ActionName")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(action.ActionName)
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

        private static List<MemberDeclarationSyntax> GenerateActionRequestFields(RGActionAttributeInfo action)
        {
            var fieldStatements = new List<MemberDeclarationSyntax>();
            foreach (var rgParameterInfo in action.Parameters)
            {
                fieldStatements.Add(
                    PropertyDeclaration(
                            IdentifierName(rgParameterInfo.Type),
                            Identifier(rgParameterInfo.Name))
                        .WithModifiers(
                            TokenList(
                                Token(SyntaxKind.PublicKeyword)))
                        .WithExpressionBody(
                            ArrowExpressionClause(
                                CastExpression(
                                    IdentifierName(rgParameterInfo.Type),
                                    ElementAccessExpression(
                                            PostfixUnaryExpression(
                                                SyntaxKind.SuppressNullableWarningExpression,
                                                IdentifierName("Input")))
                                        .WithArgumentList(
                                            BracketedArgumentList(
                                                SingletonSeparatedList(
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.StringLiteralExpression,
                                                            Literal(rgParameterInfo.Name)))))))))
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken))
                    );
            }

            return fieldStatements;
        }
        
        private static MemberDeclarationSyntax GenerateSerializationClass(string behaviourName, List<RGActionAttributeInfo> botActions)
        {
            // Generate methods for deserialization based on parameter types
            List<MemberDeclarationSyntax> methodDeclarations = new List<MemberDeclarationSyntax>();
            HashSet<string> processedTypes = new HashSet<string>();

            foreach (RGActionAttributeInfo botAction in botActions)
            {
                if (!botAction.ShouldGenerateCSFile)
                {
                    continue;
                }

                foreach (RGParameterInfo parameter in botAction.Parameters)
                {
                    if (RGUtils.IsCSharpPrimitive(parameter.Type))
                    {
                        continue;
                    }

                    if (!processedTypes.Contains(parameter.Type))
                    {
                        processedTypes.Add(parameter.Type);

                        /*
                         * Generates a method called Deserialize_{Type} for every non-primitive type
                         * Ex: Vector3
                         * public static Vector3 Deserialize_Vector3(string paramJson)
                         *    return JsonConvert.DeserializeObject<Vector3>(paramJson);
                         */
                        MethodDeclarationSyntax method = MethodDeclaration(ParseTypeName(parameter.Type), GetDeserializerMethodName(parameter))
                            .AddModifiers(Token(SyntaxKind.PublicKeyword),
                                Token(SyntaxKind.StaticKeyword))
                            .AddParameterListParameters(Parameter(Identifier("paramJson"))
                                .WithType(ParseTypeName("string")))
                            .WithBody(Block(ReturnStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParseTypeName("JsonConvert"),
                                        GenericName(Identifier("DeserializeObject"))
                                            .WithTypeArgumentList(TypeArgumentList(
                                                SingletonSeparatedList(
                                                    ParseTypeName(parameter.Type))))),
                                    ArgumentList(SingletonSeparatedList(
                                        Argument(IdentifierName("paramJson"))))))));

                        methodDeclarations.Add(method);
                    }
                }
            }

            // Create the class declaration
            ClassDeclarationSyntax classDeclaration = ClassDeclaration($"RGSerialization_{behaviourName}")
                .AddModifiers(Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword))
                .AddMembers(methodDeclarations.ToArray());

            return classDeclaration;
        }

        private static string GetDeserializerMethodName(RGParameterInfo parameter)
        {
            var result = $"Deserialize_{parameter.Type.Replace(".", "_")}";
            if (parameter.Nullable)
            {
                result = result.Replace("?", "");
                result += "_Nullable";
            }
            return result;
        }

    }
#endif
}
