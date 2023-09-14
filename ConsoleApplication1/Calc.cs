using System;
using System.Collections.Generic;
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
        public ReflectInfo(Type t, string s, Type w = null)
        {
            if(w != null) t = w;
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
        public Dictionary<double, double> range = new Dictionary<double, double>();
        public string prop;
        public double value => range.Values.First();
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
        public bool IsRange()
        {
            return !IsEmpty() && prop != null;
        }
        public bool IsEmpty()
        {
            return range.Count() == 0;
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
            catch(FormatException)
            {
                throw new CalcException(i, "Invalid expression");
            }
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
            if(props == null) props = new Dictionary<string, string>();
            if(directs != null) foreach (var p in directs) props[p] = p;
            if (props.Count == 0) return;
            Key = (makeKey) ? null : props.Values.First();
            if(!makeKey) keyDate = Key.ToLower().Contains("date");
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
            catch(CalcException e)
            {
                Console.WriteLine(e.ToString());
                //render display using the message and char position in input string
            }
            catch(Exception e)
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
        private static Result Apply(int i ,Result left, char op, Result right)
        {
            if (op == ' ' && left == null) return right;
            else if(op == ' ' || left == null) throw new CalcException(i, "Operation out of order"); //values out of order!
            Result OneRange, OneSingle;
            if(left.IsRange() && right.IsRange()) // range + range [LEFT HAND DOMINANT]
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
                var newDict = new Dictionary<double, double>();
                foreach(var x in OneRange.range.Keys) newDict[x] = SingleApply(OneRange.range[x], op, OneSingle.value);
                return new Result(newDict, OneRange.prop);
            }
            else // single + single
            {
                return new Result(SingleApply(left.value, op, right.value));
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
                    ret += Convert.ToInt32(Form[start])-48;
                    ret *= 10;
                }
                start++;
            }
            i = start - 1;
            if (dec == 1) ret /= 10;
            return new Result(ret);
        }
        private static readonly double? DNULL = (double?) null;
        private readonly Type Typ = typeof(T);
        private int propSave;
        private bool filtDate, propDate;
        private Result GetRange(int start, out int i)
        {
            List<T> filtered = Items;
            var newDict = new Dictionary<double, double>();
            propSave = start;
            string prop = Form.Substring(++start, Form.IndexOf('"', start) - start), filt;
            Result beg, end;
            double rcomp;
            double? b, e;
            bool exb = false, exe = false;

            start += prop.Length;
            if (!Props.TryGetValue(prop, out prop)) throw new CalcException(start - prop.Length, "Unrecognized field name");
            propDate = prop.ToLower().Contains("date");

            var K = new ReflectInfo(Typ, Key, null);
            var P = new ReflectInfo(Typ, prop, WrapType);
            ReflectInfo F;

            while (++start < Form.Length && Form[start] == '[')
            {
                if (Form[++start] == 'E') exb = ++start > 0;
                beg = Eval(out start, start, -2);
                if (Form[++start] == 'E') exe = ++start > 0;
                end = Eval(out start, start, -2);
                filt = Form.Substring(++start, Form.IndexOf(']', start) - start);
                start += filt.Length;
                if(!Props.TryGetValue(filt.Trim(), out filt)) throw new CalcException(start - filt.Length, "Unrecognized field name");
                filtDate = filt.ToLower().Contains("date");

                b = beg == null ? DNULL : beg.value;
                e = end == null ? DNULL : end.value;
                F = new ReflectInfo(Typ, filt.Length > 0 ? filt : prop, WrapType);
                filtered = filtered.Where(x =>
                {
                    rcomp = ConvertDouble(start, F.GetValue(x, WrapType), filtDate);
                    return (b == null || (exb ? rcomp > b : rcomp >= b)) && (e == null || (exe ? rcomp < e : rcomp <= e));
                }).ToList();
            }
            foreach (var x in filtered)
            {
                newDict[ConvertDouble(0, K.GetValue(x, null), keyDate)] = ConvertDouble(propSave, P.GetValue(x, WrapType), propDate);
            }
            i = start - 1;
            return new Result(newDict, prop);
        }

        private double keySave;
        public Result Eval(out int end, int i = 0, int func = -1)
        {
            char op = ' ';
            Result result = null;
            StringBuilder funcName = new StringBuilder("");
            for (; i < Form.Length; i++)
            {
                if (Form[i] >= 'A' && Form[i] <= 'Z')
                {
                    funcName.Append(Form[i]);
                }
                else if ((Form[i] >= '0' && Form[i] <= '9') || Form[i] == '.') // lets ignore the possibility of negative numbers for now!
                {
                    result = Apply(i, result, op, GetNum(i, out i));
                    op = ' ';
                }
                else
                {
                    switch (Form[i])
                    {
                        case ' ': break; //ignore spaces
                        case '(':
                            result = Apply(i, result, op, Eval(out i, i + 1, FuncIndex(funcName.ToString())));
                            op = ' ';
                        break;
                        case ')': case ',':
                            if(func == -2)
                            {
                                if (result != null && result.IsRange()) throw new CalcException(i, "Cannot use a range as boundary for another range");
                                end = i;
                                return result;
                            }
                            else if(Form[i] == ',') throw new CalcException(i, "Comma out of place");
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
                            else if(func > 9)
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
                            end = i + 1;
                        return result;
                        case '"':
                            result = Apply(i, result, op, GetRange(i, out i));
                            op = ' ';
                        break;
                        case '+': case '-': case '*': case '/':
                            op = Form[i];
                        break;
                    }
                    funcName.Clear();
                }
            }
            if (op != ' ') throw new CalcException(i, "Ending formula with an operator");
            end = i;
            return result;
        }
    }
}
