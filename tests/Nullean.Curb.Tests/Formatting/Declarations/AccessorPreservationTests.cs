namespace Nullean.Curb.Tests.Formatting.Declarations;

public class AccessorPreservationTests : FormattingTest
{
	private const string Multiline = """
		class C
		{
		    public int Value
		    {
		        get;
		        set;
		    }
		}
		""";

	[Test]
	public Task Multiline_auto_accessors_are_preserved_without_reflow() => Unchanged(Multiline);

	[Test]
	public Task Preservation_with_a_width_keeps_existing_accessor_breaks() =>
		Unchanged(Multiline, "max_line_length = 120\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task Canonical_layout_can_still_join_auto_accessors() => Formats(
		Multiline,
		"""
		class C
		{
		    public int Value { get; set; }
		}
		""",
		"max_line_length = 120");

	[Test]
	public Task Canonical_layout_can_join_converted_expression_accessors() => Formats(
		"""
		class C
		{
		    public int Value
		    {
		        get { return _value; }
		        set { _value = value; }
		    }
		}
		""",
		"""
		class C
		{
		    public int Value { get => _value; set => _value = value; }
		}
		""",
		"max_line_length = 120\ncsharp_style_expression_bodied_accessors = true");

	[Test]
	public Task Accessors_that_share_a_line_keep_sharing_it() => Unchanged(
		"""
		class C
		{
		    public int Value
		    {
		        get; set;
		    }
		}
		""");

	[Test]
	public Task An_indexer_preserves_expression_accessor_lines() => Unchanged(
		"""
		class C
		{
		    public int this[int index]
		    {
		        get => index;
		        set => Set(index, value);
		    }
		}
		""");

	[Test]
	public Task An_event_preserves_expression_accessor_lines() => Unchanged(
		"""
		class C
		{
		    public event Handler Changed
		    {
		        add => Add(value);
		        remove => Remove(value);
		    }
		}
		""");

	[Test]
	public Task Comments_between_accessors_remain_attached() => Unchanged(
		"""
		class C
		{
		    public int Value
		    {
		        get;
		        // Assigned by the owner.
		        private set;
		    }
		}
		""");
}
