#include "bazaararena/io/JsonLite.hpp"

#include <charconv>
#include <cmath>
#include <sstream>

namespace bazaararena::io {
namespace {

struct Parser {
    std::string_view s;
    size_t i = 0;
    JsonParseError* err = nullptr;

    char Peek() const { return i < s.size() ? s[i] : '\0'; }
    bool Eof() const { return i >= s.size(); }

    void SkipWs() {
        while (!Eof()) {
            const char c = s[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
                i++;
                continue;
            }
            break;
        }
    }

    void Fail(std::string msg) {
        if (err) {
            err->message = std::move(msg);
            err->offset = i;
        }
    }

    bool Consume(char c) {
        if (Peek() != c) return false;
        i++;
        return true;
    }

    static void AppendUtf8(std::string& out, uint32_t cp) {
        if (cp <= 0x7F) {
            out.push_back(static_cast<char>(cp));
        } else if (cp <= 0x7FF) {
            out.push_back(static_cast<char>(0xC0 | (cp >> 6)));
            out.push_back(static_cast<char>(0x80 | (cp & 0x3F)));
        } else if (cp <= 0xFFFF) {
            out.push_back(static_cast<char>(0xE0 | (cp >> 12)));
            out.push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3F)));
            out.push_back(static_cast<char>(0x80 | (cp & 0x3F)));
        } else {
            out.push_back(static_cast<char>(0xF0 | (cp >> 18)));
            out.push_back(static_cast<char>(0x80 | ((cp >> 12) & 0x3F)));
            out.push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3F)));
            out.push_back(static_cast<char>(0x80 | (cp & 0x3F)));
        }
    }

    bool ParseHex4(uint32_t& outCp) {
        if (i + 4 > s.size()) return false;
        uint32_t v = 0;
        for (int k = 0; k < 4; k++) {
            const char c = s[i + k];
            v <<= 4;
            if (c >= '0' && c <= '9') v |= static_cast<uint32_t>(c - '0');
            else if (c >= 'a' && c <= 'f') v |= static_cast<uint32_t>(10 + (c - 'a'));
            else if (c >= 'A' && c <= 'F') v |= static_cast<uint32_t>(10 + (c - 'A'));
            else return false;
        }
        i += 4;
        outCp = v;
        return true;
    }

    std::optional<std::string> ParseString() {
        if (!Consume('"')) return std::nullopt;
        std::string out;
        while (!Eof()) {
            const char c = s[i++];
            if (c == '"') return out;
            if (static_cast<unsigned char>(c) < 0x20) {
                Fail("invalid control character in string");
                return std::nullopt;
            }
            if (c != '\\') {
                out.push_back(c);
                continue;
            }
            if (Eof()) {
                Fail("unterminated escape");
                return std::nullopt;
            }
            const char e = s[i++];
            switch (e) {
                case '"': out.push_back('"'); break;
                case '\\': out.push_back('\\'); break;
                case '/': out.push_back('/'); break;
                case 'b': out.push_back('\b'); break;
                case 'f': out.push_back('\f'); break;
                case 'n': out.push_back('\n'); break;
                case 'r': out.push_back('\r'); break;
                case 't': out.push_back('\t'); break;
                case 'u': {
                    uint32_t cp = 0;
                    if (!ParseHex4(cp)) {
                        Fail("invalid \\u escape");
                        return std::nullopt;
                    }
                    // surrogate pair
                    if (cp >= 0xD800 && cp <= 0xDBFF) {
                        if (i + 2 > s.size() || s[i] != '\\' || s[i + 1] != 'u') {
                            Fail("high surrogate without low surrogate");
                            return std::nullopt;
                        }
                        i += 2;
                        uint32_t low = 0;
                        if (!ParseHex4(low) || low < 0xDC00 || low > 0xDFFF) {
                            Fail("invalid low surrogate");
                            return std::nullopt;
                        }
                        cp = 0x10000 + (((cp - 0xD800) << 10) | (low - 0xDC00));
                    }
                    AppendUtf8(out, cp);
                    break;
                }
                default:
                    Fail("invalid escape");
                    return std::nullopt;
            }
        }
        Fail("unterminated string");
        return std::nullopt;
    }

    std::optional<double> ParseNumber() {
        const size_t start = i;
        if (Peek() == '-') i++;
        if (Peek() == '0') {
            i++;
        } else {
            if (!(Peek() >= '1' && Peek() <= '9')) {
                i = start;
                return std::nullopt;
            }
            while (Peek() >= '0' && Peek() <= '9') i++;
        }
        if (Peek() == '.') {
            i++;
            if (!(Peek() >= '0' && Peek() <= '9')) {
                Fail("invalid number fraction");
                return std::nullopt;
            }
            while (Peek() >= '0' && Peek() <= '9') i++;
        }
        if (Peek() == 'e' || Peek() == 'E') {
            i++;
            if (Peek() == '+' || Peek() == '-') i++;
            if (!(Peek() >= '0' && Peek() <= '9')) {
                Fail("invalid number exponent");
                return std::nullopt;
            }
            while (Peek() >= '0' && Peek() <= '9') i++;
        }
        const auto token = s.substr(start, i - start);
        double v = 0.0;
        auto* begin = token.data();
        auto* end = token.data() + token.size();
        auto [ptr, ec] = std::from_chars(begin, end, v);
        if (ec != std::errc() || ptr != end) {
            // from_chars for double isn't fully supported on older libs; fallback.
            try {
                v = std::stod(std::string(token));
            } catch (...) {
                Fail("invalid number");
                return std::nullopt;
            }
        }
        if (!std::isfinite(v)) {
            Fail("non-finite number not allowed");
            return std::nullopt;
        }
        return v;
    }

    std::optional<JsonValue> ParseValue() {
        SkipWs();
        const char c = Peek();
        if (c == '"') {
            auto s = ParseString();
            if (!s) return std::nullopt;
            return JsonValue(std::move(*s));
        }
        if (c == '{') return ParseObject();
        if (c == '[') return ParseArray();
        if (c == 't') {
            if (s.substr(i, 4) == "true") { i += 4; return JsonValue(true); }
            Fail("invalid literal");
            return std::nullopt;
        }
        if (c == 'f') {
            if (s.substr(i, 5) == "false") { i += 5; return JsonValue(false); }
            Fail("invalid literal");
            return std::nullopt;
        }
        if (c == 'n') {
            if (s.substr(i, 4) == "null") { i += 4; return JsonValue(nullptr); }
            Fail("invalid literal");
            return std::nullopt;
        }
        auto num = ParseNumber();
        if (num) return JsonValue(*num);
        Fail("unexpected token");
        return std::nullopt;
    }

    std::optional<JsonValue> ParseArray() {
        if (!Consume('[')) return std::nullopt;
        JsonArray arr;
        SkipWs();
        if (Consume(']')) return JsonValue(std::move(arr));
        while (true) {
            auto v = ParseValue();
            if (!v) return std::nullopt;
            arr.push_back(std::move(*v));
            SkipWs();
            if (Consume(']')) break;
            if (!Consume(',')) {
                Fail("expected ',' or ']'");
                return std::nullopt;
            }
        }
        return JsonValue(std::move(arr));
    }

    std::optional<JsonValue> ParseObject() {
        if (!Consume('{')) return std::nullopt;
        JsonObject obj;
        SkipWs();
        if (Consume('}')) return JsonValue(std::move(obj));
        while (true) {
            SkipWs();
            auto k = ParseString();
            if (!k) {
                Fail("expected string key");
                return std::nullopt;
            }
            SkipWs();
            if (!Consume(':')) {
                Fail("expected ':'");
                return std::nullopt;
            }
            auto v = ParseValue();
            if (!v) return std::nullopt;
            obj.emplace(std::move(*k), std::move(*v));
            SkipWs();
            if (Consume('}')) break;
            if (!Consume(',')) {
                Fail("expected ',' or '}'");
                return std::nullopt;
            }
        }
        return JsonValue(std::move(obj));
    }
};

static void EscapeString(std::ostringstream& os, const std::string& s) {
    os << '"';
    for (unsigned char c : s) {
        switch (c) {
            case '"': os << "\\\""; break;
            case '\\': os << "\\\\"; break;
            case '\b': os << "\\b"; break;
            case '\f': os << "\\f"; break;
            case '\n': os << "\\n"; break;
            case '\r': os << "\\r"; break;
            case '\t': os << "\\t"; break;
            default:
                if (c < 0x20) {
                    static const char* hex = "0123456789ABCDEF";
                    os << "\\u00" << hex[(c >> 4) & 0xF] << hex[c & 0xF];
                } else {
                    os << static_cast<char>(c);
                }
        }
    }
    os << '"';
}

static void Stringify(std::ostringstream& os, const JsonValue& v) {
    if (std::holds_alternative<std::nullptr_t>(v.v)) {
        os << "null";
    } else if (auto* b = std::get_if<bool>(&v.v)) {
        os << (*b ? "true" : "false");
    } else if (auto* n = std::get_if<double>(&v.v)) {
        if (std::floor(*n) == *n && std::abs(*n) <= 9e15) {
            os << static_cast<long long>(*n);
        } else {
            os << *n;
        }
    } else if (auto* s = std::get_if<std::string>(&v.v)) {
        EscapeString(os, *s);
    } else if (auto* a = std::get_if<JsonArray>(&v.v)) {
        os << '[';
        for (size_t i = 0; i < a->size(); i++) {
            if (i) os << ',';
            Stringify(os, (*a)[i]);
        }
        os << ']';
    } else if (auto* o = std::get_if<JsonObject>(&v.v)) {
        os << '{';
        bool first = true;
        for (const auto& [k, vv] : *o) {
            if (!first) os << ',';
            first = false;
            EscapeString(os, k);
            os << ':';
            Stringify(os, vv);
        }
        os << '}';
    }
}

}  // namespace

std::optional<JsonValue> ParseJson(std::string_view text, JsonParseError& err) {
    Parser p{.s = text, .i = 0, .err = &err};
    err = JsonParseError{};
    auto v = p.ParseValue();
    if (!v) return std::nullopt;
    p.SkipWs();
    if (!p.Eof()) {
        err.message = "trailing characters";
        err.offset = p.i;
        return std::nullopt;
    }
    return v;
}

std::string StringifyJson(const JsonValue& v) {
    std::ostringstream os;
    Stringify(os, v);
    return os.str();
}

const JsonValue* GetObjectField(const JsonValue& obj, std::string_view key) {
    const auto* o = obj.AsObject();
    if (!o) return nullptr;
    auto it = o->find(key);
    if (it == o->end()) return nullptr;
    return &it->second;
}

const JsonValue* GetArrayIndex(const JsonValue& arr, size_t idx) {
    const auto* a = arr.AsArray();
    if (!a) return nullptr;
    if (idx >= a->size()) return nullptr;
    return &(*a)[idx];
}

std::optional<std::string_view> GetString(const JsonValue& v) {
    if (auto* s = v.AsString()) return std::string_view(*s);
    return std::nullopt;
}

std::optional<double> GetNumber(const JsonValue& v) {
    if (auto* n = v.AsNumber()) return *n;
    return std::nullopt;
}

std::optional<int> GetInt(const JsonValue& v) {
    auto n = GetNumber(v);
    if (!n) return std::nullopt;
    const double d = *n;
    if (std::floor(d) != d) return std::nullopt;
    if (d < static_cast<double>(std::numeric_limits<int>::min()) ||
        d > static_cast<double>(std::numeric_limits<int>::max())) {
        return std::nullopt;
    }
    return static_cast<int>(d);
}

std::optional<bool> GetBool(const JsonValue& v) {
    if (auto* b = v.AsBool()) return *b;
    return std::nullopt;
}

}  // namespace bazaararena::io

