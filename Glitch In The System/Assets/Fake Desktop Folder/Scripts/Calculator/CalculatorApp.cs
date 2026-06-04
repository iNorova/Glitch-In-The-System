using UnityEngine;
using TMPro;

/// <summary>
/// Old-Windows-style calculator logic for the fake desktop.
/// Attach to CalculatorAppWindow. Wire displayText in Inspector.
/// Display shows live expression: "5 + 2" while typing, result on "=".
/// </summary>
public sealed class CalculatorApp : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI displayText;

    private double _pendingOperand;
    private string _pendingOp       = "";
    private bool   _awaitingOperand = true;

    // Numeric portion of what the user is currently typing
    private string _currentEntry    = "0";
    // Full expression shown on display e.g. "5 + 2"
    private string _expression      = "0";

    // Symbol map: internal op → display glyph
    private static string OpGlyph(string op)
    {
        switch (op)
        {
            case "+": return "+";
            case "-": return "-";
            case "*": return "×";
            case "/": return "÷";
            default:  return op;
        }
    }

    private void Awake() => Refresh();

    // ── Number / decimal input ─────────────────────────────────────────────
    public void InputDigit(int digit)
    {
        if (_awaitingOperand)
        {
            _currentEntry    = digit.ToString();
            _awaitingOperand = false;
        }
        else
        {
            _currentEntry = (_currentEntry == "0") ? digit.ToString()
                                                   : _currentEntry + digit;
        }

        RebuildExpression();
        Refresh();
    }

    public void InputDecimal()
    {
        if (_awaitingOperand)
        {
            _currentEntry    = "0.";
            _awaitingOperand = false;
        }
        else if (!_currentEntry.Contains("."))
        {
            _currentEntry += ".";
        }

        RebuildExpression();
        Refresh();
    }

    // ── Operators ──────────────────────────────────────────────────────────
    public void InputOperator(string op)
    {
        double current = ParseEntry();

        // Chain: evaluate previous pending op first
        if (!_awaitingOperand && _pendingOp != "")
            current = Evaluate(_pendingOperand, current, _pendingOp);

        _pendingOperand  = current;
        _pendingOp       = op;
        _awaitingOperand = true;
        _currentEntry    = Format(current);

        // Show e.g. "5 +"  (operator replaces if pressed again)
        _expression = _currentEntry + " " + OpGlyph(op);
        Refresh();
    }

    public void InputEquals()
    {
        if (_pendingOp == "")
        {
            // Nothing pending — just show current entry
            _expression = _currentEntry;
            Refresh();
            return;
        }

        double result    = Evaluate(_pendingOperand, ParseEntry(), _pendingOp);
        _pendingOp       = "";
        _awaitingOperand = true;
        _currentEntry    = Format(result);
        _expression      = _currentEntry;
        Refresh();
    }

    // ── Clear ──────────────────────────────────────────────────────────────
    public void Clear()
    {
        _pendingOperand  = 0;
        _pendingOp       = "";
        _awaitingOperand = true;
        _currentEntry    = "0";
        _expression      = "0";
        Refresh();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// Rebuild _expression from pending operand + pending op + current entry.
    private void RebuildExpression()
    {
        if (_pendingOp != "")
            _expression = Format(_pendingOperand) + " " + OpGlyph(_pendingOp) + " " + _currentEntry;
        else
            _expression = _currentEntry;
    }

    private double ParseEntry()
    {
        double.TryParse(_currentEntry,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double v);
        return v;
    }

    private static double Evaluate(double a, double b, string op)
    {
        switch (op)
        {
            case "+": return a + b;
            case "-": return a - b;
            case "*": return a * b;
            case "/": return (b == 0) ? 0 : a / b;
            default:  return b;
        }
    }

    private static string Format(double v)
    {
        string s = (v == System.Math.Floor(v) && System.Math.Abs(v) < 1e12)
            ? ((long)v).ToString()
            : v.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);
        return s.Length > 14
            ? v.ToString("G8", System.Globalization.CultureInfo.InvariantCulture)
            : s;
    }

    private void Refresh()
    {
        if (displayText != null) displayText.text = _expression;
    }
}
