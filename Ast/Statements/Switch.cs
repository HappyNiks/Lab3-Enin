using System.Collections.Generic;
using System.Linq;
namespace Lab3.Ast.Statements {
	sealed class Switch : IStatement {
		public readonly IExpression Condition;
		public readonly IReadOnlyList<SwitchBody> SwitchBodies;
		public Switch(IExpression condition, IReadOnlyList<SwitchBody> switchBodies) {
			Condition = condition;
			SwitchBodies = switchBodies;
		}
		public string FormattedString => $"switch ({Condition.FormattedString}) " + "{\n" + string.Join("", SwitchBodies.Select(x => x.FormattedString)) + "}\n";
		public void Accept(IStatementVisitor visitor) => visitor.VisitSwitch(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitSwitch(this);
	}
}
