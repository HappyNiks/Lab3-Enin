namespace Lab3.Ast {
	sealed class SwitchBody : INode {
		public readonly IExpression CaseValue;
		public readonly Block Block;
		public SwitchBody(IExpression caseValue, Block block) {
			CaseValue = caseValue;
			Block = block;
		}
		public string FormattedString {
			get {
				if (CaseValue == null) {
					return $"{Block.FormattedString}";
				}
				else {
					return $"{CaseValue.FormattedString}: {Block.FormattedString}";
				}
			}
		}
	}
}
