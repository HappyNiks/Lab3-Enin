﻿namespace Lab3.Ast {
	interface IStatement : INode {
		void Accept(IStatementVisitor visitor);
		T Accept<T>(IStatementVisitor<T> visitor);
	}
}
