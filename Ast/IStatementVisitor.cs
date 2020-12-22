using Lab3.Ast.Statements;
namespace Lab3.Ast {
	interface IStatementVisitor {
		void VisitIf(If ifStatement);
		void VisitWhile(While whileStatement);
		void VisitSwitch(Switch switchStatement);
		void VisitExpressionStatement(ExpressionStatement expressionStatement);
		void VisitVariableDeclaration(VariableDeclaration variableDeclaration);
		void VisitVariableAssignment(VariableAssignment variableAssignment);
	}
	interface IStatementVisitor<T> {
		T VisitIf(If ifStatement);
		T VisitWhile(While whiteStatement);
		T VisitSwitch(Switch switchStatement);
		T VisitExpressionStatement(ExpressionStatement expressionStatement);
		T VisitVariableDeclaration(VariableDeclaration variableDeclaration);
		T VisitVariableAssignment(VariableAssignment variableAssignment);
	}
}
