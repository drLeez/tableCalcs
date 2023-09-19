using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ConsoleApplication1
{
    [Serializable]
    internal class CalcException : Exception
    {
        public int Pos;
        public CalcException(int pos, string message) : base(message) { Pos = pos; }
        public CalcException(int pos, string message, Exception innerException) : base(message, innerException) { Pos = pos; }
        public override string ToString() => base.ToString() + " : " + Pos;
    }
    internal class CalcWrapper<T>
    {
        public T Obj;
        public double CalcKey;
        public CalcWrapper(T o, double k)
        {
            Obj = o;
            CalcKey = k;
        }
    }
    internal class ReflectInfo
    {
        public FieldInfo f;
        public PropertyInfo p;
        public ReflectInfo(Type t, string s)
        {
            f = t.GetField(s);
            if (f == null) p = t.GetProperty(s);
        }
        public object GetValue(object o, Type w)
        {
            if (w != null) o = o.GetType().GetField("Obj").GetValue(o);
            return p == null ? f.GetValue(o) : p.GetValue(o, null);
        }
    }
    public class Result
    {
        public static readonly Dictionary<double, double> SPECDICT = new Dictionary<double, double>();
        public Dictionary<double, double> range = new Dictionary<double, double>();
        public string prop;
        public double value => range.Values.First();

        public static bool UnknownValue(int i, Result r, out object ret)
        {
            //if (r.IsRange() || r.IsString()) throw new CalcException(i, "");
            if (r.range == SPECDICT)
            {
                ret = r.prop;
                return false;
            }
            ret = r.value;
            return true;
        }

        public Result(Dictionary<double, double> rr, string p) //set range
        {
            if (p == null) throw new Exception();
            prop = p;
            range = rr;
        }
        public Result(double v) //set single
        {
            prop = null;
            range.Clear();
            range[0] = v;
        }
        public bool IsString()
        {
            return range == null && prop != null;
        }
        public bool IsRange()
        {
            return range != null && range != SPECDICT && prop != null;
        }
    }
    public class Calc<T>
    {
        private static readonly string[] FuncNames = new string[] { "SUM", "AVG", "MED", "MODE", null, null, null, null, null, null,
                                                                    "ROUND", "FLOOR", "CEIL", "NEG"};
        private static int FuncIndex(string name)
        {
            for (int i = 0; i < FuncNames.Length; i++) if (FuncNames[i] == name) return i;
            return -1;
        }
        private static readonly string Comps = "<>!";
        private static int CompIndex(StringBuilder s)
        {
            if (s.Length == 0 || (s.Length == 1 && s[0] == '!') || (s.Length == 2 && s[1] != '=')) return -1;
            return Comps.IndexOf(s[0]) + (s.Length == 1 ? 4 : 0);
        }
        private static bool IsComp(char c) => c == '<' || c == '>' || c == '!' || c == '=';
        private static bool ConvertUnknown(int i, object o, bool date, out object ret)
        {
            if (date || (Nullable.GetUnderlyingType(o.GetType()) ?? o.GetType()).IsPrimitive)
            {
                ret = ConvertDouble(i, o, date);
                return true;
            }
            ret = o.ToString();
            return false;
        }
        private static double ConvertDouble(int i, object o, bool date)
        {
            try
            {
                if (date)
                {
                    var ret = DateTime.ParseExact(o.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    return Convert.ToDouble(ret.Date.ToShortDateString()); //this doesnt work
                }
                return Convert.ToDouble(o);
            }
            catch (FormatException)
            {
                throw new CalcException(i, "Invalid expression");
            }
        }
        private List<T> Merge(List<T> a, List<T> b, ReflectInfo K)
        {
            bool p;
            double x;
            foreach (var i in b)
            {
                p = true;
                x = ConvertDouble(0, K.GetValue(i, null), false);
                foreach (var n in a) if (ConvertDouble(0, K.GetValue(n, null), false) == x) { p = false; break; }
                if (p) a.Add(i);
            }
            return a;
        }

        private List<T> Items;
        private Type WrapType = null;
        private string Form;
        private readonly string Key;
        //private readonly string[] Props;
        private readonly Dictionary<string, string> Props;
        private readonly bool keyDate;
        public Calc(Dictionary<string, string> props, string[] directs = null, bool makeKey = false)
        {
            if (props == null) props = new Dictionary<string, string>();
            if (directs != null) foreach (var p in directs) props[p] = p;
            if (props.Count == 0) return;
            Key = makeKey ? null : props.Values.First();
            if (!makeKey) keyDate = Key.ToLower().Contains("date");
            Props = props;
        }
        public Calc(string[] excludes = null, bool makeKey = false)
        {
            var props = new Dictionary<string, string>();
            foreach (var f in Typ.GetFields()) if (excludes == null || !excludes.Contains(f.Name)) props[f.Name] = f.Name;
            foreach (var p in Typ.GetProperties()) if (excludes == null || !excludes.Contains(p.Name)) props[p.Name] = p.Name;
            if (props.Count == 0) return;
            Key = makeKey ? null : props.Values.First();
            if (!makeKey) keyDate = Key.ToLower().Contains("date");
            Props = props;
        }
        public Result Do(string form, List<T> items = null)
        {
            if (items.Count == 0) return null;
            if (Key == null)
            {
                var newProps = new Dictionary<string, string>();
                newProps["CalcKey"] = "CalcKey";
                foreach (var k in Props.Keys) newProps[k] = Props[k];
                var newItems = new List<CalcWrapper<T>>();
                double count = 0;
                foreach (var i in items) newItems.Add(new CalcWrapper<T>(i, count++));
                var newCalc = new Calc<CalcWrapper<T>>(newProps) { WrapType = items.First().GetType() };
                return newCalc.Do(form, newItems);
            }
            Items = items;
            Form = form;
            try
            {
                return Eval(out int i);
            }
            catch (CalcException e)
            {
                Console.WriteLine(e.ToString());
                //render display using the message and char position in input string
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //render display with generic "Calculation Error."
                throw;
            }
            return null;
        }
        private static double SingleApply(double left, char op, double right)
        {
            switch (op)
            {
                case '+': return left + right;
                case '-': return left - right;
                case '*': return left * right;
                case '/': return left / right;
                case '%': return left % right;
            }
            return 0;
        }
        private static double hasValue;
        private static Result Apply(int i, Result left, char op, Result right)
        {
            if (op == ZCHAR && left == null) return right;
            else if (op == ZCHAR || left == null) throw new CalcException(i, "Operation out of order"); //values out of order!
            Result OneRange, OneSingle;
            if (left.IsRange() && right.IsRange()) // range + range [LEFT HAND DOMINANT]
            {
                var newDict = new Dictionary<double, double>();
                foreach (var x in left.range.Keys)
                {
                    if (right.range.TryGetValue(x, out hasValue)) newDict[x] = SingleApply(left.range[x], op, right.range[x]);
                }
                return new Result(newDict, left.prop);
            }
            else if ((OneRange = left.IsRange() ? left : null) != null || (OneRange = right.IsRange() ? right : null) != null) // single + range
            {
                if (OneRange == right && (op == 1 || op == 3)) throw new CalcException(i, "Cannot subtract or divide a range from a single value");
                OneSingle = OneRange == right ? left : right;
                if (OneSingle.IsString()) throw new CalcException(i, "Cannot apply a string value on a range");
                var newDict = new Dictionary<double, double>();
                foreach (var x in OneRange.range.Keys) newDict[x] = SingleApply(OneRange.range[x], op, OneSingle.value);
                return new Result(newDict, OneRange.prop);
            }
            else // single + single
            {
                if (left.IsString() || right.IsString()) throw new CalcException(i, "Cannot apply operation on string values");
                return new Result(SingleApply(left.value, op, right.value));
            }
        }
        private static bool CompString(string left, int comp, string right)
        {
            switch (comp)
            {
                case 2: return left != right;
                case 3: return left == right;
                default: throw new ArgumentException();
            }
        }
        private static bool CompDouble(double left, int comp, double right)
        {
            switch (comp)
            {
                case 0: return left <= right;
                case 1: return left >= right;
                case 2: return left != right;
                case 3: return left == right;
                case 4: return left < right;
                case 5: return left > right;
                default: throw new ArgumentException();
            }
        }
        private static bool Comp(int i, bool lDub, object lcomp, int ct, bool rDub, object rcomp)
        {
            if (lDub ^ rDub) throw new CalcException(i, "Comparing string and numeric values");
            try
            {
                if (lDub) return CompDouble((double)lcomp, ct, (double)rcomp);
                return CompString((string)lcomp, ct, (string)rcomp);
            }
            catch (ArgumentException)
            {
                throw new CalcException(i, "Invalid comparison operator");
            }
        }

        private Result GetNum(int start, out int i)
        {
            int dec = 1;
            double ret = 0;
            while (start < Form.Length && ((Form[start] >= '0' && Form[start] <= '9') || Form[start] == '.'))
            {
                if (Form[start] == '.')
                {
                    ret /= 10;
                    dec = 10;
                }
                else if (dec > 1)
                {
                    ret += (Convert.ToInt32(Form[start]) - 48) / dec;
                    dec *= 10;
                }
                else
                {
                    ret += Convert.ToInt32(Form[start]) - 48;
                    ret *= 10;
                }
                start++;
            }
            i = start - 1;
            if (dec == 1) ret /= 10;
            return new Result(ret);
        }
        private static readonly double? DNULL = (double?)null;
        private readonly Type Typ = typeof(T);
        private readonly StringBuilder comp = new StringBuilder(2);
        private int propSave;
        private bool lDate, rDate, propDate;
        private Result GetRange(int start, out int i)
        {
            List<T> filtered = Items, newFiltered;
            var newDict = new Dictionary<double, double>();
            propSave = start;
            string prop = Form.Substring(++start, Form.IndexOf('\'', start) - start);
            Result beg, end;
            object lcomp, rcomp;
            bool lDub, rDub, inc;

            start += prop.Length;
            if (!Props.TryGetValue(prop, out prop)) throw new CalcException(start - prop.Length, "Unrecognized field name");
            propDate = prop.ToLower().Contains("date");

            var K = new ReflectInfo(Typ, Key);
            var P = new ReflectInfo(WrapType ?? Typ, prop);
            string ls, rs;
            int ct;
            ReflectInfo L, R;

            while (++start < Form.Length && (Form[start] == '[' || Form[start] == '|'))
            {
                inc = Form[start] == '|';
                comp.Clear();
                beg = Eval(out start, ++start, -2);
                if (!beg.IsString() && !beg.IsRange()) start--;
                while (++start < Form.Length && (Form[start] == ' ' || IsComp(Form[start])))
                    try
                    {
                        if (Form[start] != ' ') comp.Append(Form[start]);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw new CalcException(start, "Invalid comparison symbol");
                    }
                if ((ct = CompIndex(comp)) == -1) throw new CalcException(start, "Invalid comparison symbol");
                end = Eval(out start, start, -2);

                if (beg.IsString() && end.IsString()) // range + range comp
                {
                    if (beg == SPECRES) ls = prop;
                    else if (!Props.TryGetValue(beg.prop.Trim(), out ls)) throw new CalcException(start, "Unrecognized field name");
                    if (end == SPECRES)
                    {
                        start++;
                        rs = prop;
                    }
                    else if (!Props.TryGetValue(end.prop.Trim(), out rs)) throw new CalcException(start, "Unrecognized field name");
                    if (ls == rs) throw new CalcException(start, "Comparing field to itself");
                    lDate = ls.ToLower().Contains("date");
                    rDate = rs.ToLower().Contains("date");
                    L = new ReflectInfo(WrapType ?? Typ, ls);
                    R = new ReflectInfo(WrapType ?? Typ, rs);
                    newFiltered = (inc ? Items : filtered).Where(x =>
                    {
                        lDub = ConvertUnknown(start, L.GetValue(x, WrapType), lDate, out lcomp);
                        rDub = ConvertUnknown(start, R.GetValue(x, WrapType), rDate, out rcomp);
                        return Comp(start, lDub, lcomp, ct, rDub, rcomp);
                    }).ToList();
                    filtered = inc ? Merge(filtered, newFiltered, K) : newFiltered;
                }
                else if (beg.IsString() || end.IsString()) // range + single comp
                {
                    var temp = beg.IsString() ? beg : end;
                    if (temp == SPECRES) ls = prop;
                    else if (!Props.TryGetValue(temp.prop.Trim(), out ls)) throw new CalcException(start, "Unrecognized field name");
                    lDate = ls.ToLower().Contains("date");
                    L = new ReflectInfo(WrapType ?? Typ, ls);
                    rDub = Result.UnknownValue(start, beg.IsString() ? end : beg, out rcomp);
                    if (end.IsString()) // need to flip ct
                    {
                        if (ct < 2) ct = -(ct - 1);
                        else if (ct > 3) ct = 4 - (ct - 5);
                        start++;
                    }
                    newFiltered = (inc ? Items : filtered).Where(x =>
                    {
                        lDub = ConvertUnknown(start, L.GetValue(x, WrapType), lDate, out lcomp);
                        return Comp(start, lDub, lcomp, ct, rDub, rcomp);
                    }).ToList();
                    filtered = inc ? Merge(filtered, newFiltered, K) : newFiltered;
                }
                else throw new CalcException(start, "Cannot compare two single values for range bounds");
            }
            foreach (var x in filtered)
            {
                newDict[ConvertDouble(0, K.GetValue(x, null), keyDate)] = ConvertDouble(propSave, P.GetValue(x, WrapType), propDate);
            }
            i = start - 1;
            return new Result(newDict, prop);
        }

        private static readonly Result SPECRES = new Result(null, "");
        private static readonly char ZCHAR = (char)0;
        private double keySave;
        public Result Eval(out int end, int i = 0, int func = -1)
        {
            char op = ZCHAR;
            Result result = null;
            StringBuilder word = new StringBuilder("");
            for (; i < Form.Length; i++)
            {
                if ((Form[i] >= 'a' && Form[i] <= 'z') || (Form[i] >= 'A' && Form[i] <= 'Z'))
                {
                    word.Append(Form[i]);
                }
                else if ((Form[i] >= '0' && Form[i] <= '9') || Form[i] == '.') // lets ignore the possibility of negative numbers for now!
                {
                    result = Apply(i, result, op, GetNum(i, out i));
                    op = ZCHAR;
                }
                else
                {
                    switch (Form[i])
                    {
                        case ' ': continue; //ignore spaces (keep word built)
                        case '[': throw new CalcException(i, "Bracket out of place");
                        case '(':
                            result = Apply(i, result, op, Eval(out i, i + 1, FuncIndex(word.ToString())));
                            op = ZCHAR;
                            break;
                        case ')':
                        case '<':
                        case '>':
                        case '=':
                        case '!':
                        case ']':
                        case '|':
                            if (func == -2)
                            {
                                //if (result != null && result.IsRange()) throw new CalcException(i, "Cannot use a range as boundary for another range");
                                if (result == null && word.ToString() == "this")
                                {
                                    i--;
                                    result = SPECRES;
                                }
                                end = i;
                                return result;
                            }
                            else if (Form[i] != ')') throw new CalcException(i, "Comparing outside of range definition");
                            if (result == null) throw new CalcException(i, "Reached closing parenthesis without value");
                            if (result.IsRange()) switch (func)
                                {
                                    //REDUCE//
                                    case 0: // SUM
                                        var sum = result.range.Values.Sum();
                                        result = new Result(sum);
                                        break;
                                    case 1: // AVG (mean)
                                        var avg = result.range.Values.Sum() / result.range.Count();
                                        result = new Result(avg);
                                        break;
                                    case 2: // MED
                                        var med = result.range.Values.OrderBy(x => x).ToList();
                                        result = new Result(med[med.Count / 2]);
                                        break;
                                    case 3: // MODE
                                        var counts = new Dictionary<double, int>();
                                        foreach (var x in result.range.Values)
                                            if (counts.ContainsKey(x)) counts[x]++;
                                            else counts[x] = 1;
                                        var mode = counts.Aggregate(new KeyValuePair<double, int>(0, 0), (m, x) => m.Value > x.Value ? m : x);
                                        result = new Result(mode.Key);
                                        break;
                                    //APPLY EACH//
                                    case 10: // ROUND
                                        foreach (var k in result.range.Keys) result.range[k] = Math.Round(result.range[k]);
                                        break;
                                    case 11: // FLOOR
                                        foreach (var k in result.range.Keys) result.range[k] = Math.Floor(result.range[k]);
                                        break;
                                    case 12: // CEIL
                                        foreach (var k in result.range.Keys) result.range[k] = Math.Ceiling(result.range[k]);
                                        break;
                                    case 13: // NEG
                                        foreach (var k in result.range.Keys) result.range[k] = -result.range[k];
                                        break;
                                }
                            else if (func > 9)
                            {
                                keySave = result.range.Keys.First();
                                switch (func)
                                {
                                    case 10: // ROUND
                                        result.range[keySave] = Math.Round(result.range[keySave]);
                                        break;
                                    case 11: // FLOOR
                                        result.range[keySave] = Math.Floor(result.range[keySave]);
                                        break;
                                    case 12: // CEIL
                                        result.range[keySave] = Math.Ceiling(result.range[keySave]);
                                        break;
                                    case 13: // NEG
                                        result.range[keySave] = -result.range[keySave];
                                        break;
                                }
                            }
                            else throw new CalcException(i, "Cannot apply reducing functions to single values");
                            end = i;
                            return result;
                        case '\'':
                            if (func == -2)
                            {
                                var filt = Form.Substring(++i, Form.IndexOf('\'', i) - i);
                                result = new Result(null, filt.Trim());
                                end = i + filt.Length;
                                return result;
                            }
                            else result = Apply(i, result, op, GetRange(i, out i));
                            op = ZCHAR;
                            break;
                        case '"':
                            var str = Form.Substring(++i, Form.IndexOf('"', i) - i);
                            result = Apply(i, result, op, new Result(Result.SPECDICT, str.Trim()));
                            i += str.Length;
                            op = ZCHAR;
                            break;
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                            op = Form[i];
                            break;
                    }
                    word.Clear();
                }
            }
            if (op != ZCHAR) throw new CalcException(i, "Ending formula with an operator");
            end = i;
            return result;
        }
    }
}
