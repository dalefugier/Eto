using System;

namespace Eto.Forms
{
	public abstract class SingleValueCell : Cell
	{
		public SingleBinding Binding { get; set; }
		
		protected SingleValueCell (Generator g, Type type, bool initialize)
			: base(g, type, initialize)
		{
		}
	}
}

