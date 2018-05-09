using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace XmlToTable.Core
{
    internal class Parser
    {
        private static readonly Dictionary<Type, Parser> ParserCache = new Dictionary<Type, Parser>();

        private readonly bool _isNumericType;
        private readonly MethodInfo _parseMethod;
        private readonly MethodInfo _tryParseMethod;

        private Parser(MethodInfo parseMethod, MethodInfo tryParseMethod, bool isNumericType)
        {
            _parseMethod = parseMethod;
            _tryParseMethod = tryParseMethod;
            _isNumericType = isNumericType;
        }

        public bool CanParse(string value)
        {
            if (IsNumeric(value))
            {
                value = RemoveSpaces(value);
            }

            List<object> parserParameters = new List<object> { value };

            if (_isNumericType)
            {
                NumberStyles style = NumberStyles.Number;
                style &= ~NumberStyles.AllowTrailingSign;
                parserParameters.Add(style);
                parserParameters.Add(NumberFormatInfo.CurrentInfo);
            }

            parserParameters.Add(null);

            return (bool)_tryParseMethod.Invoke(null, parserParameters.ToArray());
        }

        private static bool IsNumeric(string value)
        {
            return Regex.IsMatch(value, @"^\s*[-+]?\s*\d*(\.\d+)?\s*$");
        }

        public object Parse(string value)
        {
            if (IsNumeric(value))
            {
                value = RemoveSpaces(value);
            }

            return _parseMethod.Invoke(null, new object[] { value });
        }

        private static string RemoveSpaces(string value)
        {
            return value.Replace(" ", string.Empty);
        }

        public static Parser GetParser<T>()
        {
            return GetParser(typeof(T));
        }

        public static Parser GetParser(Type type)
        {
            return ParserCache.ContainsKey(type)
                ? ParserCache[type]
                : GetParserInternal(type);
        }

        private static Parser GetParserInternal(Type type)
        {
            MethodInfo basicTry = null;
            MethodInfo numericTry = null;
            MethodInfo parseMethod = null;

            MethodInfo[] publicStaticMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
            foreach (MethodInfo methodInfo in publicStaticMethods)
            {
                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (methodInfo.Name == "TryParse")
                {
                    ParameterInfo parameter = null;
                    if (parameters.Length >= 2)
                    {
                        parameter = parameters[1];
                    }
                    if (parameter != null)
                    {
                        if (parameter.IsOut)
                        {
                            basicTry = methodInfo;
                        }
                        else if (parameter.ParameterType == typeof(NumberStyles))
                        {
                            numericTry = methodInfo;
                        }
                    }
                }
                else if (methodInfo.Name == "Parse")
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        parseMethod = methodInfo;
                    }
                }
            }

            Parser parser = null;
            if (parseMethod != null)
            {
                if (numericTry != null)
                {
                    parser = new Parser(parseMethod, numericTry, true);
                }
                else if (basicTry != null)
                {
                    parser = new Parser(parseMethod, basicTry, false);
                }
            }

            ParserCache.Add(type, parser);

            return parser;
        }
    }
}