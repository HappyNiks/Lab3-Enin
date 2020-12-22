﻿using DynamicRuntime;
using Lab3.Ast;
using Lab3.Ast.Expressions;
using Lab3.Ast.Statements;
using Lab3.Parsing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Globalization;
namespace Lab3.DynamicCompiling {
	sealed class Compiler : IStatementVisitor, IExpressionVisitor {
		static readonly VariableDefinition missingVariable = null;
		SourceFile sourceFile;
		readonly MethodDefinition method;
		readonly ModuleDefinition module;
		readonly ILProcessor cil;
		readonly Dictionary<string, VariableDefinition> variables = new Dictionary<string, VariableDefinition>();
		Dictionary<string, VariableDefinition> currentBlockShadowedVariables = null;
		public Compiler(MethodDefinition method) {
			this.method = method;
			module = method.Module;
			cil = method.Body.GetILProcessor();
		}
		public void CompileProgram(ProgramNode program) {
			sourceFile = program.SourceFile;
			try {
				foreach (var field in typeof(BuiltinVariables).GetFields()) {
					if (field.FieldType != typeof(object)) {
						continue;
					}
					var variable = new VariableDefinition(module.TypeSystem.Object);
					method.Body.Variables.Add(variable);
					variables[field.Name] = variable;
					cil.Emit(OpCodes.Ldsfld, module.ImportReference(field));
					cil.Emit(OpCodes.Stloc, variable);
				}
				foreach (var statement in program.Statements) {
					CompileStatement(statement);
				}
				cil.Emit(OpCodes.Ret);
			}
			finally {
				sourceFile = null;
				variables.Clear();
			}
		}
		void EmitRuntimeCall(string methodName) {
			var method = typeof(Op).GetMethod(methodName);
			if (method == null) {
				throw new Exception($"{methodName} не найден");
			}
			var methodReference = module.ImportReference(method);
			cil.Emit(OpCodes.Call, methodReference);
		}
		void CompileBlock(Block block) {
			var oldShadowedVariables = currentBlockShadowedVariables;
			currentBlockShadowedVariables = new Dictionary<string, VariableDefinition>();
			foreach (var statement in block.Statements) {
				CompileStatement(statement);
			}
			foreach (var kv in currentBlockShadowedVariables) {
				var name = kv.Key;
				var shadowedVariable = kv.Value;
				if (shadowedVariable == missingVariable) {
					variables.Remove(name);
				}
				else {
					variables[name] = shadowedVariable;
				}
			}
			currentBlockShadowedVariables = oldShadowedVariables;
		}
		#region statements
		void CompileStatement(IStatement statement) {
			statement.Accept(this);
		}
		public void VisitIf(If ifStatement) {
			CompileExpression(ifStatement.Condition);
			EmitRuntimeCall(nameof(Op.ToBool));
			var afterIf = Instruction.Create(OpCodes.Nop);
			cil.Emit(OpCodes.Brfalse, afterIf);
			CompileBlock(ifStatement.Body);
			cil.Append(afterIf);
		}
		public void VisitWhile(While whileStatement) {
			var loop = Instruction.Create(OpCodes.Nop);
			var afterLoop = Instruction.Create(OpCodes.Nop);
			cil.Append(loop);
			CompileExpression(whileStatement.Condition);
			EmitRuntimeCall(nameof(Op.ToBool));
			cil.Emit(OpCodes.Brfalse, afterLoop);
			CompileBlock(whileStatement.Body);
			cil.Emit(OpCodes.Br, loop);
			cil.Append(afterLoop);
		}
		public void VisitSwitch(Switch switchStatement) {
			CompileExpression(switchStatement.Condition);
			var condition = new VariableDefinition(module.TypeSystem.Object);
			method.Body.Variables.Add(condition);
			cil.Emit(OpCodes.Stloc, condition);
			var afterSwitch = Instruction.Create(OpCodes.Nop);
			foreach (var caseBody in switchStatement.SwitchBodies) {
				if (caseBody.CaseValue != null) {
					cil.Emit(OpCodes.Ldloc, condition);
					CompileExpression(caseBody.CaseValue);
					EmitRuntimeCall(nameof(Op.Eq));
					EmitRuntimeCall(nameof(Op.ToBool));
					var afterCase = Instruction.Create(OpCodes.Nop);
					cil.Emit(OpCodes.Brfalse, afterCase);
					CompileBlock(caseBody.Block);
					cil.Emit(OpCodes.Br, afterSwitch);
					cil.Append(afterCase);
				}
				else {
					CompileBlock(caseBody.Block);
					cil.Emit(OpCodes.Br, afterSwitch);
				}
			}
			cil.Append(afterSwitch);
		}
		public void VisitExpressionStatement(ExpressionStatement expressionStatement) {
			CompileExpression(expressionStatement.Expr);
			cil.Emit(OpCodes.Pop);
		}
		public void VisitVariableDeclaration(VariableDeclaration variableDeclaration) {
			CompileExpression(variableDeclaration.Expr);
			var name = variableDeclaration.VariableName;
			if (currentBlockShadowedVariables != null && !currentBlockShadowedVariables.ContainsKey(name)) {
				if (variables.TryGetValue(name, out var existingVariable)) {
					currentBlockShadowedVariables[name] = existingVariable;
				}
				else {
					currentBlockShadowedVariables[name] = missingVariable;
				}
			}
			var variable = new VariableDefinition(module.TypeSystem.Object);
			method.Body.Variables.Add(variable);
			variables[name] = variable;
			cil.Emit(OpCodes.Stloc, variable);
		}
		public void VisitVariableAssignment(VariableAssignment variableAssignment) {
			if (!variables.TryGetValue(variableAssignment.VariableName, out var variable)) {
				throw MakeError(variableAssignment.Expr, $"Присваивание в неизвестную переменную {variableAssignment.VariableName}");
			}
			CompileExpression(variableAssignment.Expr);
			cil.Emit(OpCodes.Stloc, variable);
		}
		#endregion
		#region expressions
		void CompileExpression(IExpression expression) {
			expression.Accept(this);
		}
		public void VisitBinary(Binary binary) {
			CompileExpression(binary.Left);
			CompileExpression(binary.Right);
			if (binary.OperatorString == "+") {
				EmitRuntimeCall(nameof(Op.Add));
			}
			else if (binary.OperatorString == "-") {
				EmitRuntimeCall(nameof(Op.Sub));
			}
			else if (binary.OperatorString == "*") {
				EmitRuntimeCall(nameof(Op.Mul));
			}
			else if (binary.OperatorString == "/") {
				EmitRuntimeCall(nameof(Op.Div));
			}
			else if (binary.OperatorString == "%") {
				EmitRuntimeCall(nameof(Op.Rem));
			}
			else if (binary.OperatorString == "<") {
				EmitRuntimeCall(nameof(Op.Lt));
			}
			else if (binary.OperatorString == "==") {
				EmitRuntimeCall(nameof(Op.Eq));
			}
			else {
				throw MakeError(binary, $"Неизвестная операция {binary.OperatorString}");
			}
		}
		public void VisitCall(Call call) {
			CompileExpression(call.Function);
			cil.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
			cil.Emit(OpCodes.Newarr, module.TypeSystem.Object);
			var index = 0;
			foreach (var arg in call.Arguments) {
				cil.Emit(OpCodes.Dup);
				cil.Emit(OpCodes.Ldc_I4, index);
				CompileExpression(arg);
				cil.Emit(OpCodes.Stelem_Ref);
				index++;
			}
			EmitRuntimeCall(nameof(Op.Call));
		}
		public void VisitParentheses(Parentheses parentheses) {
			CompileExpression(parentheses.Expr);
		}
		public void VisitNumber(Number number) {
			if (!int.TryParse(number.Lexeme, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int value)) {
				throw MakeError(number, $"Не получилось преобразовать {number.Lexeme} к типу Int32");
			}
			cil.Emit(OpCodes.Ldc_I4, value);
			cil.Emit(OpCodes.Box, module.TypeSystem.Int32);
		}
		public void VisitIdentifier(Identifier identifier) {
			if (!variables.TryGetValue(identifier.Name, out var variable)) {
				throw MakeError(identifier, $"Неизвестная переменная {identifier.Name}");
			}
			cil.Emit(OpCodes.Ldloc, variable);
		}
		public void VisitMemberAccess(MemberAccess memberAccess) {
			throw new NotSupportedException();
		}
		#endregion
		Exception MakeError(IExpression expression, string message) {
			return new Exception(sourceFile.MakeErrorMessage(expression.Position, message));
		}
	}
}
