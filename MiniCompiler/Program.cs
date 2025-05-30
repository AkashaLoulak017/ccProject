// Updated Mini Compiler with float support and better parsing

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MiniCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Enter source code (end with an empty line):");
                string line;
                string inputCode = "";

                while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
                {
                    inputCode += line + "\n";
                }

                // Lexical Analysis
                Console.WriteLine("\n=== LEXICAL ANALYSIS ===");
                var lexer = new Lexer();
                var tokens = lexer.Tokenize(inputCode);

                Console.WriteLine("\n--- Tokens ---");
                foreach (var token in tokens)
                    Console.WriteLine(token);

                // Syntax Analysis
                Console.WriteLine("\n=== SYNTAX ANALYSIS ===");
                var parser = new Parser();
                var ast = parser.Parse(tokens);

                // Semantic Analysis
                Console.WriteLine("\n=== SEMANTIC ANALYSIS ===");
                var semanticAnalyzer = new SemanticAnalyzer();
                semanticAnalyzer.Analyze(ast);

                // Intermediate Code Generation
                Console.WriteLine("\n=== INTERMEDIATE CODE GENERATION ===");
                var codeGen = new IntermediateCodeGenerator();
                var ir = codeGen.Generate(ast);

                Console.WriteLine("\n--- Intermediate Code ---");
                foreach (var lineCode in ir)
                    Console.WriteLine(lineCode);

                // Symbol Table
                Console.WriteLine("\n=== SYMBOL TABLE ===");
                SymbolTable.Instance.Print();
            }
            catch (CompilerException ex)
            {
                Console.WriteLine($"\nCOMPILATION FAILED: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Details: {ex.InnerException.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nUNEXPECTED ERROR: {ex.Message}");
            }
        }
    }

    class CompilerException : Exception
    {
        public CompilerException(string message) : base(message) { }
        public CompilerException(string message, Exception inner) : base(message, inner) { }
    }

    enum TokenType { Keyword, Identifier, Number, FloatNumber, Operator, Assignment, Separator, Unknown }

    class Token
    {
        public TokenType Type;
        public string Value;
        public Token(TokenType type, string value)
        {
            Type = type; Value = value;
        }
        public override string ToString() => $"[{Type}] {Value}";
    }

    class Lexer
    {
        string[] keywords = { "int", "float", "print" };
        public List<Token> Tokenize(string code)
        {
            var tokens = new List<Token>();
            var pattern = @"(?<Keyword>\bint\b|\bfloat\b|\bprint\b)|" +
              @"(?<Identifier>[a-zA-Z_][a-zA-Z0-9_]*)|" +
              @"(?<FloatNumber>\d+\.\d+)|" +
              @"(?<Number>\d+)|" +
              @"(?<Assignment>=)|" +
              @"(?<Operator>[+\-*/])|" +
              @"(?<Separator>[;()])";

            var regex = new Regex(pattern);
            var lines = code.Split('\n');

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum];
                if (string.IsNullOrWhiteSpace(line)) continue;

                Console.WriteLine($"\nLexing line {lineNum + 1}: {line.Trim()}");
                var matches = regex.Matches(line);
                foreach (Match match in matches)
                {
                    foreach (var groupName in regex.GetGroupNames())
                    {
                        if (groupName == "0") continue;
                        if (match.Groups[groupName].Success)
                        {
                            TokenType type = Enum.TryParse(groupName, out TokenType parsedType) ? parsedType : TokenType.Unknown;
                            tokens.Add(new Token(type, match.Value));
                            Console.WriteLine($"  Found token: {type} '{match.Value}'");
                            break;
                        }
                    }
                }
            }
            return tokens;
        }
    }

    class ASTNode
    {
        public string Type;
        public string Value;
        public List<ASTNode> Children = new List<ASTNode>();
        public ASTNode(string type, string value = "")
        {
            Type = type; Value = value;
        }

        public void Print(int indent = 0)
        {
            Console.WriteLine($"{new string(' ', indent * 2)}{Type}: {Value}");
            foreach (var child in Children)
                child.Print(indent + 1);
        }
    }

    class Parser
    {
        List<Token> tokens;
        int position;

        public ASTNode Parse(List<Token> tokens)
        {
            this.tokens = tokens;
            position = 0;
            var root = new ASTNode("Program");

            try
            {
                while (position < tokens.Count)
                {
                    var stmt = ParseStatement();
                    if (stmt != null)
                        root.Children.Add(stmt);
                }
            }
            catch (Exception ex)
            {
                throw new CompilerException($"Syntax error at position {position}: {ex.Message}", ex);
            }

            Console.WriteLine("\n--- Abstract Syntax Tree ---");
            root.Print();
            return root;
        }

        ASTNode ParseStatement()
        {
            if (Match(TokenType.Keyword, "int") || Match(TokenType.Keyword, "float"))
            {
                string type = tokens[position - 1].Value;
                Console.WriteLine($"\n[Syntax] Parsing {type} variable declaration...");
                var decl = new ASTNode("Declaration", type);
                var id = Expect(TokenType.Identifier);
                decl.Children.Add(new ASTNode("Identifier", id.Value));
                Console.WriteLine($"  Found identifier: {id.Value}");

                if (Match(TokenType.Assignment))
                {
                    Console.WriteLine("  Found assignment, parsing expression...");
                    var expr = ParseExpression();
                    decl.Children.Add(expr);
                }

                Expect(TokenType.Separator, ";");
                Console.WriteLine("  End of declaration statement");
                return decl;
            }
            else if (Peek().Type == TokenType.Identifier && Peek(position + 1)?.Type == TokenType.Assignment)
            {
                Console.WriteLine("\n[Syntax] Parsing assignment statement...");
                var id = Expect(TokenType.Identifier);
                Expect(TokenType.Assignment);
                var assign = new ASTNode("Assignment", "=");
                assign.Children.Add(new ASTNode("Identifier", id.Value));
                assign.Children.Add(ParseExpression());
                Expect(TokenType.Separator, ";");
                Console.WriteLine("  End of assignment statement");
                return assign;
            }
            else if (Match(TokenType.Keyword, "print"))
            {
                Console.WriteLine("\n[Syntax] Parsing print statement...");
                var print = new ASTNode("Print", "print");
                var id = Expect(TokenType.Identifier);
                print.Children.Add(new ASTNode("Identifier", id.Value));
                Console.WriteLine($"  Found identifier to print: {id.Value}");
                Expect(TokenType.Separator, ";");
                Console.WriteLine("  End of print statement");
                return print;
            }

            throw new Exception($"Unexpected token: {Peek()?.Value}");
        }

        ASTNode ParseExpression()
        {
            Console.WriteLine("  Parsing expression...");
            var left = ParseTerm();

            while (Match(TokenType.Operator, "+") || Match(TokenType.Operator, "-"))
            {
                var op = tokens[position - 1];
                Console.WriteLine($"  Found additive operator: {op.Value}");
                var node = new ASTNode("BinaryOp", op.Value);
                node.Children.Add(left);
                node.Children.Add(ParseTerm());
                left = node;
            }
            return left;
        }

        ASTNode ParseTerm()
        {
            var left = ParseFactor();
            while (Match(TokenType.Operator, "*") || Match(TokenType.Operator, "/"))
            {
                var op = tokens[position - 1];
                Console.WriteLine($"  Found multiplicative operator: {op.Value}");
                var node = new ASTNode("BinaryOp", op.Value);
                node.Children.Add(left);
                node.Children.Add(ParseFactor());
                left = node;
            }
            return left;
        }

        ASTNode ParseFactor()
        {
            if (Peek().Type == TokenType.Number || Peek().Type == TokenType.FloatNumber)
            {
                var num = tokens[position++];
                Console.WriteLine($"  Found number: {num.Value}");
                return new ASTNode("Number", num.Value);
            }
            else if (Peek().Type == TokenType.Identifier)
            {
                var id = Expect(TokenType.Identifier);
                Console.WriteLine($"  Found identifier in expression: {id.Value}");
                return new ASTNode("Identifier", id.Value);
            }
            else
                throw new Exception("Expected identifier or number in expression");
        }

        bool Match(TokenType type, string value = null)
        {
            if (position < tokens.Count && tokens[position].Type == type && (value == null || tokens[position].Value == value))
            {
                position++;
                return true;
            }
            return false;
        }

        Token Expect(TokenType type, string value = null)
        {
            if (position < tokens.Count && tokens[position].Type == type && (value == null || tokens[position].Value == value))
                return tokens[position++];
            throw new Exception($"Expected {type} '{value ?? "<any>"}', found: {Peek()?.Value ?? "EOF"}");
        }

        Token Peek(int ahead = 0) => position + ahead < tokens.Count ? tokens[position + ahead] : null;
    }

    class SemanticAnalyzer
    {
        public void Analyze(ASTNode root)
        {
            Console.WriteLine("\nStarting semantic analysis...");

            try
            {
                foreach (var node in root.Children)
                {
                    if (node.Type == "Declaration")
                    {
                        string type = node.Value;
                        string id = node.Children[0].Value;
                        Console.WriteLine($"\nAnalyzing declaration of variable '{id}' as {type}");

                        if (SymbolTable.Instance.Exists(id))
                        {
                            throw new CompilerException($"Variable '{id}' already declared.");
                        }

                        string value = null;
                        if (node.Children.Count > 1)
                        {
                            Console.WriteLine($"  Checking initialization expression...");
                            value = EvaluateExpression(node.Children[1]);
                            Console.WriteLine($"  Initial value: {value ?? "null"}");
                        }

                        SymbolTable.Instance.Add(id, type, value);
                        Console.WriteLine($"  Added '{id}' to symbol table as {type}");
                    }
                    else if (node.Type == "Assignment")
                    {
                        string id = node.Children[0].Value;
                        Console.WriteLine($"\nAnalyzing assignment to variable '{id}'");

                        if (!SymbolTable.Instance.Exists(id))
                        {
                            throw new CompilerException($"Undeclared variable '{id}'");
                        }

                        string type = SymbolTable.Instance.GetType(id);
                        string value = EvaluateExpression(node.Children[1]);
                        Console.WriteLine($"  Assigned value: {value}");

                        // Update symbol table with new value
                        SymbolTable.Instance.UpdateValue(id, value);
                    }
                    else if (node.Type == "Print")
                    {
                        string id = node.Children[0].Value;
                        Console.WriteLine($"\nAnalyzing print statement for variable '{id}'");

                        if (!SymbolTable.Instance.Exists(id))
                        {
                            throw new CompilerException($"Undeclared variable '{id}'");
                        }

                        Console.WriteLine($"  Variable '{id}' is valid for printing");
                    }
                }

                Console.WriteLine("Semantic analysis completed successfully");
            }
            catch (CompilerException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CompilerException("Semantic analysis failed", ex);
            }
        }

        string EvaluateExpression(ASTNode node)
        {
            try
            {
                if (node.Type == "Number")
                {
                    Console.WriteLine($"    Found number literal: {node.Value}");
                    return node.Value;
                }

                if (node.Type == "Identifier")
                {
                    Console.WriteLine($"    Found variable reference: {node.Value}");
                    if (!SymbolTable.Instance.Exists(node.Value))
                    {
                        throw new CompilerException($"Undeclared variable '{node.Value}'");
                    }

                    string value = SymbolTable.Instance.GetValue(node.Value);
                    Console.WriteLine($"    Variable '{node.Value}' has value: {value ?? "null"}");
                    return value;
                }

                if (node.Type == "BinaryOp")
                {
                    Console.WriteLine($"    Evaluating binary operation: {node.Value}");
                    string left = EvaluateExpression(node.Children[0]);
                    string right = EvaluateExpression(node.Children[1]);

                    if (left == null || right == null)
                    {
                        throw new CompilerException($"Cannot perform operation on uninitialized variables");
                    }

                    Console.WriteLine($"    Operation: {left} {node.Value} {right}");
                    return $"{left} {node.Value} {right}";
                }

                return null;
            }
            catch (CompilerException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CompilerException("Expression evaluation failed", ex);
            }
        }
    }

    class IntermediateCodeGenerator
    {
        int tempCount = 0;

        public List<string> Generate(ASTNode root)
        {
            var code = new List<string>();
            Console.WriteLine("\nGenerating intermediate code...");

            try
            {
                foreach (var node in root.Children)
                {
                    if (node.Type == "Declaration")
                    {
                        string type = node.Value;
                        string id = node.Children[0].Value;
                        if (node.Children.Count > 1)
                        {
                            Console.WriteLine($"  Processing {type} declaration with initialization: {id}");
                            string exprResult = GenerateExpression(node.Children[1], code);
                            code.Add($"{id} = {exprResult}");
                            Console.WriteLine($"    Generated: {id} = {exprResult}");
                        }
                        else
                        {
                            Console.WriteLine($"  Processing {type} declaration without initialization: {id}");
                            code.Add($"DECLARE {id} as {type}");
                            Console.WriteLine($"    Generated: DECLARE {id} as {type}");
                        }
                    }
                    else if (node.Type == "Assignment")
                    {
                        string id = node.Children[0].Value;
                        Console.WriteLine($"  Processing assignment to {id}");
                        string exprResult = GenerateExpression(node.Children[1], code);
                        code.Add($"{id} = {exprResult}");
                        Console.WriteLine($"    Generated: {id} = {exprResult}");
                    }
                    else if (node.Type == "Print")
                    {
                        string id = node.Children[0].Value;
                        code.Add($"PRINT {id}");
                        Console.WriteLine($"  Generated print statement for {id}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CompilerException("Intermediate code generation failed", ex);
            }

            return code;
        }

        string GenerateExpression(ASTNode node, List<string> code)
        {
            if (node.Type == "Number")
            {
                return node.Value;
            }

            if (node.Type == "Identifier")
            {
                return node.Value;
            }

            if (node.Type == "BinaryOp")
            {
                string left = GenerateExpression(node.Children[0], code);
                string right = GenerateExpression(node.Children[1], code);
                string temp = $"t{tempCount++}";
                code.Add($"{temp} = {left} {node.Value} {right}");
                Console.WriteLine($"    Generated temp operation: {temp} = {left} {node.Value} {right}");
                return temp;
            }

            throw new CompilerException($"Unsupported node type in expression: {node.Type}");
        }
    }

    class SymbolTable
    {
        private Dictionary<string, (string type, string value)> table = new();
        private static SymbolTable _instance;
        public static SymbolTable Instance => _instance ??= new SymbolTable();

        public void Add(string name, string type, string value)
        {
            table[name] = (type, value);
        }

        public void UpdateValue(string name, string value)
        {
            if (table.ContainsKey(name))
            {
                table[name] = (table[name].type, value);
            }
        }

        public bool Exists(string name) => table.ContainsKey(name);

        public string GetValue(string name) => table.TryGetValue(name, out var entry) ? entry.value : null;

        public string GetType(string name) => table.TryGetValue(name, out var entry) ? entry.type : null;

        public void Print()
        {
            if (table.Count == 0)
            {
                Console.WriteLine("Symbol table is empty");
                return;
            }

            Console.WriteLine("Name\tType\tValue");
            foreach (var entry in table)
                Console.WriteLine($"{entry.Key}\t{entry.Value.type}\t{entry.Value.value ?? "null"}");
        }
    }
}