using System;
using Eto.Forms;
using Eto.Drawing;
using Eto.Mac.Drawing;
using System.Text.RegularExpressions;
using System.Linq;

#if XAMMAC2
using AppKit;
using Foundation;
using CoreGraphics;
using ObjCRuntime;
using CoreAnimation;
using CoreImage;
#else
using MonoMac.AppKit;
using MonoMac.Foundation;
using MonoMac.CoreGraphics;
using MonoMac.ObjCRuntime;
using MonoMac.CoreAnimation;
using MonoMac.CoreImage;
#if Mac64
using CGSize = MonoMac.Foundation.NSSize;
using CGRect = MonoMac.Foundation.NSRect;
using CGPoint = MonoMac.Foundation.NSPoint;
using nfloat = System.Double;
using nint = System.Int64;
using nuint = System.UInt64;
#else
using CGSize = System.Drawing.SizeF;
using CGRect = System.Drawing.RectangleF;
using CGPoint = System.Drawing.PointF;
using nfloat = System.Single;
using nint = System.Int32;
using nuint = System.UInt32;
#endif
#endif

#if XAMMAC2
using nnint = System.nint;
#elif Mac64
using nnint = System.UInt64;
#else
using nnint = System.Int32;
#endif

namespace Eto.Mac.Forms.Controls
{

	public class EtoLabelFieldCell : NSTextFieldCell
	{
		public EtoLabelFieldCell()
		{
		}

		public EtoLabelFieldCell(IntPtr handle)
			: base(handle)
		{
		}

		[Export("verticalAlignment")]
		public VerticalAlignment VerticalAlignment { get; set; }

		public override CGRect DrawingRectForBounds(CGRect theRect)
		{
			var rect = base.DrawingRectForBounds(theRect);
			var titleSize = CellSizeForBounds(theRect);

			switch (VerticalAlignment)
			{
				case VerticalAlignment.Center:
					rect.Y = (nfloat)Math.Round(theRect.Y + (theRect.Height - titleSize.Height) / 2.0F);
					break;
				case VerticalAlignment.Bottom:
					rect.Y = theRect.Y + (theRect.Height - titleSize.Height);
					break;
			}
			return rect;
		}
	}

	public class EtoLabel : NSTextField, IMacControl
	{
		public WeakReference WeakHandler { get; set; }

		public object Handler
		{ 
			get { return WeakHandler.Target; }
			set { WeakHandler = new WeakReference(value); } 
		}

		public EtoLabel()
		{
			Cell = new EtoLabelFieldCell();
			DrawsBackground = false;
			Bordered = false;
			Bezeled = false;
			Editable = false;
			Selectable = false;
			Alignment = NSTextAlignment.Left;
		}
	}

	public abstract class MacLabel<TControl, TWidget, TCallback> : MacView<TControl, TWidget, TCallback>
		where TControl: NSTextField
		where TWidget: Control
		where TCallback: Control.ICallback
	{
		static readonly bool supportsSingleLine;
		readonly NSMutableAttributedString str;
		readonly NSMutableParagraphStyle paragraphStyle;
		int underlineIndex;
		SizeF availableSizeCached;
		const NSStringDrawingOptions DrawingOptions = NSStringDrawingOptions.UsesFontLeading | NSStringDrawingOptions.UsesLineFragmentOrigin;

		static MacLabel()
		{
			supportsSingleLine = ObjCExtensions.ClassInstancesRespondToSelector(Class.GetHandle("NSTextFieldCell"), Selector.GetHandle("setUsesSingleLineMode:"));
		}

		public override NSView ContainerControl { get { return Control; } }

		#if !XAMMAC2
		static readonly Selector selAlignmentRectInsets = new Selector("alignmentRectInsets");
		#endif

		protected override SizeF GetNaturalSize(SizeF availableSize)
		{
			if (NaturalSize == null || availableSizeCached != availableSize)
			{
				#if XAMMAC2 // TODO: Fix when Xamarin.Mac2 NSEdgeInsets is fixed to use nfloat instead of float
				var insets = new Size(4, 2);
				#else
				var insets = Control.RespondsToSelector(selAlignmentRectInsets) ? Control.AlignmentRectInsets.ToEtoSize() : new Size(4, 2);
				#endif
				var size = Control.Cell.CellSizeForBounds(new CGRect(CGPoint.Empty, availableSize.ToNS())).ToEto();

				NaturalSize = Size.Round(size + insets);
				availableSizeCached = availableSize;
			}

			return NaturalSize.Value;
		}

		public MacLabel()
		{
			Enabled = true;
			paragraphStyle = new NSMutableParagraphStyle();
			str = new NSMutableAttributedString();

			underlineIndex = -1;
			paragraphStyle.LineBreakMode = NSLineBreakMode.ByWordWrapping;
		}

		protected override void Initialize()
		{
			if (supportsSingleLine)
				Control.Cell.UsesSingleLineMode = false;

			base.Initialize();
		}

		protected override TControl CreateControl()
		{
			return new EtoLabel() as TControl;
		}

		static readonly object TextColorKey = new object();

		public Color TextColor
		{
			get { return Widget.Properties.Get<Color?>(TextColorKey) ?? NSColor.Text.ToEto(); }
			set
			{
				if (value != TextColor)
				{
					Widget.Properties[TextColorKey] = value;
					SetAttributes();
				}
			}
		}

		public WrapMode Wrap
		{
			get
			{
				if (supportsSingleLine && Control.Cell.UsesSingleLineMode)
					return WrapMode.None;
				if (paragraphStyle.LineBreakMode == NSLineBreakMode.ByWordWrapping)
					return WrapMode.Word;
				return WrapMode.Character;
			}
			set
			{
				switch (value)
				{
					case WrapMode.None:
						if (supportsSingleLine)
							Control.Cell.UsesSingleLineMode = true;
						paragraphStyle.LineBreakMode = NSLineBreakMode.Clipping;
						break;
					case WrapMode.Word:
						if (supportsSingleLine)
							Control.Cell.UsesSingleLineMode = false;
						paragraphStyle.LineBreakMode = NSLineBreakMode.ByWordWrapping;
						break;
					case WrapMode.Character:
						if (supportsSingleLine)
							Control.Cell.UsesSingleLineMode = false;
						paragraphStyle.LineBreakMode = NSLineBreakMode.CharWrapping;
						break;
					default:
						throw new NotSupportedException();
				}
				SetAttributes();
			}
		}

		public override bool Enabled { get; set; }

		public string Text
		{
			get { return str.Value; }
			set
			{
				var oldSize = GetPreferredSize(Size.MaxValue);
				if (string.IsNullOrEmpty(value))
				{
					str.SetString(new NSMutableAttributedString());
				}
				else
				{
					var match = Regex.Match(value, @"(?<=([^&](?:[&]{2})*)|^)[&](?![&])");
					if (match.Success)
					{
						var val = value.Remove(match.Index, match.Length).Replace("&&", "&");

						var matches = Regex.Matches(value, @"[&][&]");
						var prefixCount = matches.Cast<Match>().Count(r => r.Index < match.Index);

						str.SetString(new NSAttributedString(val));
						underlineIndex = match.Index - prefixCount;
					}
					else
					{
						str.SetString(new NSAttributedString(value.Replace("&&", "&")));
						underlineIndex = -1;
					}
				}
				SetAttributes();
				LayoutIfNeeded(oldSize);
			}
		}

		public TextAlignment TextAlignment
		{
			get { return paragraphStyle.Alignment.ToEto(); }
			set
			{
				paragraphStyle.Alignment = value.ToNS();
				SetAttributes();
			}
		}

		static readonly object FontKey = new object();

		public virtual Font Font
		{
			get
			{
				return Widget.Properties.Create<Font>(FontKey, () => new Font(new FontHandler(Control.Font)));
			}
			set
			{
				if (Widget.Properties.Get<Font>(FontKey) != value)
				{
					var oldSize = GetPreferredSize(Size.MaxValue);
					Widget.Properties[FontKey] = value;
					SetAttributes();
					LayoutIfNeeded(oldSize);
				}
			}
		}

		public VerticalAlignment VerticalAlignment
		{
			get { return ((EtoLabelFieldCell)Control.Cell).VerticalAlignment; }
			set { ((EtoLabelFieldCell)Control.Cell).VerticalAlignment = value; }
		}

		protected virtual void SetAttributes()
		{
			SetAttributes(false);
		}

		void SetAttributes(bool force)
		{
			if (Widget.Loaded || force)
			{
				if (str.Length > 0)
				{
					var range = new NSRange(0, (int)str.Length);
					var attr = new NSMutableDictionary();
					Widget.Properties.Get<Font>(FontKey).Apply(attr);
					attr.Add(NSStringAttributeKey.ParagraphStyle, paragraphStyle);
					var col = Widget.Properties.Get<Color?>(TextColorKey);
					if (col != null)
						attr.Add(NSStringAttributeKey.ForegroundColor, col.Value.ToNSUI());
					//attr.Add(NSStringAttributeKey.ForegroundColor, CurrentColor);
					str.SetAttributes(attr, range);
					if (underlineIndex >= 0)
					{
						var num = (NSNumber)str.GetAttribute(NSStringAttributeKey.UnderlineStyle, (nnint)underlineIndex, out range);
						var newStyle = (num != null && (NSUnderlineStyle)num.Int64Value == NSUnderlineStyle.Single) ? NSUnderlineStyle.Double : NSUnderlineStyle.Single;
						str.AddAttribute(NSStringAttributeKey.UnderlineStyle, new NSNumber((int)newStyle), new NSRange(underlineIndex, 1));
					}
				}
				Control.AttributedStringValue = str;
			}
		}

		protected virtual NSColor CurrentColor
		{
			get { return TextColor.ToNSUI(); }
		}

		public override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			SetAttributes(true);
		}

		public override void AttachEvent(string id)
		{
			switch (id)
			{
				case TextControl.TextChangedEvent:
					break;
				default:
					base.AttachEvent(id);
					break;
			}
		}
	}
}
