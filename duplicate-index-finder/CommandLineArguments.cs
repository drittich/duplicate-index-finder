using DotArguments;
using DotArguments.Attributes;

using System;

namespace duplicate_index_finder
{
    class CommandLineArguments
	{
		[NamedValueArgument("schema", 's', IsOptional = true)]
		[ArgumentDescription(Short = "the schema name to filter indexes by")]
		public string Schema { get; set; }

		[NamedValueArgument("table", 't', IsOptional = true)]
		[ArgumentDescription(Short = "the table name to filter indexes by")]
		public string Table { get; set; }

        [NamedValueArgument("index", 'i', IsOptional = true)]
        [ArgumentDescription(Short = "the index name to filter indexes by")]
        public string Index { get; set; }

        [NamedSwitchArgument("help", 'h')]
        [ArgumentDescription(Short = "get help text")]
        public bool Help { get; set; }

        [RemainingArguments]
		public string[] RemainingArguments { get; set; }

		public static CommandLineArguments ParseArguments(string[] argArray)
		{
			// create container definition and the parser
			var definition = new ArgumentDefinition(typeof(CommandLineArguments));
			var parser = new GNUArgumentParser();

			CommandLineArguments arguments = null;
			try
			{
				// create object with the populated arguments
				arguments = parser.Parse<CommandLineArguments>(definition, argArray);
			}
			catch (System.Exception ex)
			{
				Console.Error.WriteLine(string.Format("Error: {0}", ex.Message));
				Console.Error.Write(GetUsageString());

				//throw;
			}

			return arguments;
		}

		public static string GetUsageString()
		{
			var definition = new ArgumentDefinition(typeof(CommandLineArguments));
			var parser = new GNUArgumentParser();
			return string.Format("Usage: {0}", parser.GenerateUsageString(definition));
		}
	}

}
