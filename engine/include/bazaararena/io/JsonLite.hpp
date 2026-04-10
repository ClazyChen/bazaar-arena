#pragma once

#include <cstddef>
#include <cstdint>
#include <map>
#include <optional>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

namespace bazaararena::io {

struct JsonValue;
using JsonObject = std::map<std::string, JsonValue, std::less<>>;
using JsonArray = std::vector<JsonValue>;

struct JsonValue {
    using Storage = std::variant<std::nullptr_t, bool, double, std::string, JsonArray, JsonObject>;
    Storage v;

    JsonValue() : v(nullptr) {}
    JsonValue(std::nullptr_t) : v(nullptr) {}
    JsonValue(bool b) : v(b) {}
    JsonValue(double d) : v(d) {}
    JsonValue(std::string s) : v(std::move(s)) {}
    JsonValue(const char* s) : v(std::string(s)) {}
    JsonValue(JsonArray a) : v(std::move(a)) {}
    JsonValue(JsonObject o) : v(std::move(o)) {}

    bool IsNull() const { return std::holds_alternative<std::nullptr_t>(v); }
    bool IsBool() const { return std::holds_alternative<bool>(v); }
    bool IsNumber() const { return std::holds_alternative<double>(v); }
    bool IsString() const { return std::holds_alternative<std::string>(v); }
    bool IsArray() const { return std::holds_alternative<JsonArray>(v); }
    bool IsObject() const { return std::holds_alternative<JsonObject>(v); }

    const bool* AsBool() const { return std::get_if<bool>(&v); }
    const double* AsNumber() const { return std::get_if<double>(&v); }
    const std::string* AsString() const { return std::get_if<std::string>(&v); }
    const JsonArray* AsArray() const { return std::get_if<JsonArray>(&v); }
    const JsonObject* AsObject() const { return std::get_if<JsonObject>(&v); }
};

struct JsonParseError {
    std::string message;
    size_t offset = 0;
};

// 解析 UTF-8 JSON 文本（支持 object/array/string/number/bool/null）。
std::optional<JsonValue> ParseJson(std::string_view text, JsonParseError& err);

// 将 JsonValue 序列化为紧凑 JSON（UTF-8）。
std::string StringifyJson(const JsonValue& v);

// 读取与访问帮助函数（不抛异常）
const JsonValue* GetObjectField(const JsonValue& obj, std::string_view key);
const JsonValue* GetArrayIndex(const JsonValue& arr, size_t i);

std::optional<std::string_view> GetString(const JsonValue& v);
std::optional<double> GetNumber(const JsonValue& v);
std::optional<int> GetInt(const JsonValue& v);
std::optional<bool> GetBool(const JsonValue& v);

}  // namespace bazaararena::io

