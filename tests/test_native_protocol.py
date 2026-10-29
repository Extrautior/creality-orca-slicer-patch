"""Compile actual ported C++ sections with small network/config fixtures.

No alternate Python implementation of CFS parsing or flush arithmetic is tested.
The GUI/network shell is replaced; the production method bodies are used verbatim.
This is a focused regression test, not a full Orca application build.
"""
import argparse
from pathlib import Path
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, required=True)
parser.add_argument('--compiler', default='g++')
parser.add_argument('--build', type=Path, default=Path('build/native-tests'))
args = parser.parse_args()
source = args.source.resolve()
cpp = (source/'src/slic3r/Utils/CrealityPrint.cpp').read_text(encoding='utf-8')
hpp = (source/'src/slic3r/Utils/CrealityPrint.hpp').read_text(encoding='utf-8')
gcode = (source/'src/libslic3r/GCode.cpp').read_text(encoding='utf-8')
slot = re.search(r'    struct CfsSlotInfo \{.*?\n    \};', hpp, re.S).group()
helpers = cpp[cpp.index('static bool parse_int('):cpp.index('static int get_extended_info_int(')]
helpers += cpp[cpp.index('static int json_int_value('):cpp.index('static std::string creality_material_color(')]
query = cpp[cpp.index('std::vector<CrealityPrint::CfsSlotInfo> CrealityPrint::query_cfs_slots('):cpp.index('bool CrealityPrint::set_cfs_slot_filament(')]
models = cpp[cpp.index('static const std::map<std::string, std::string>& cfs_capable_models()'):cpp.index('bool CrealityPrint::supports_multi_color_print()')]
commands = cpp[cpp.index('std::vector<std::string> CrealityPrint::start_print_commands('):cpp.index('bool CrealityPrint::start_print(')]
matrix_start = gcode.index('    { // correct the flush_volumes_matrix', gcode.index('void GCode::append_full_config('))
matrix_end = gcode.index('    // filament_map_2', matrix_start)
matrix = gcode[matrix_start:matrix_end]

prefix = r'''
#include <nlohmann/json.hpp>
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <map>
#include <stdexcept>
#include <string>
#include <type_traits>
#include <vector>
using json = nlohmann::json;
#define BOOST_LOG_TRIVIAL(level) std::cerr
#define L(x) x
#define _(x) x
namespace Slic3r { using SlicingError = std::runtime_error; }
void require(bool pass, const char* message) { if (!pass) { std::cerr << "FAIL: " << message << std::endl; std::exit(1); } }
'''
host = 'class CrealityPrint {\npublic:\n'+slot+r'''
    std::string response;
    std::string query_boxes_info() const { return response; }
    std::vector<CfsSlotInfo> query_cfs_slots(bool include_external_spool) const;
    static bool model_supports_multi_color(const std::string& model);
    static std::string model_display_name(const std::string& model);
    static std::vector<std::string> start_print_commands(bool multi_color, const std::string& path, const std::map<std::string, std::string>& info);
};
'''
config = r'''
enum class PrimeVolumeMode { pvmDefault, pvmFast };
struct ConfigOptionFloats { std::vector<double> values; };
struct ConfigOptionStrings { std::vector<std::string> values; };
struct FixtureConfig {
    std::map<std::string, ConfigOptionFloats> floats;
    ConfigOptionStrings colors;
    template<class T> T* option(const std::string& name) {
        if constexpr (std::is_same_v<T, ConfigOptionFloats>) return &floats.at(name);
        else return &colors;
    }
};
struct FixturePrint {
    PrimeVolumeMode prime_volume_mode;
    const FixturePrint& config() const { return *this; }
};
FixtureConfig export_matrix(FixtureConfig cfg, PrimeVolumeMode mode, bool is_creality_print_host, bool is_creality_hi) {
    FixturePrint print{mode};
'''+matrix+'\n    return cfg;\n}\n'
tests = r'''
int main() {
    require(CrealityPrint::model_supports_multi_color("F018") && CrealityPrint::model_display_name("F018") == "Hi", "Hi is present in the shared discovery/capability table");
    require(CrealityPrint::model_supports_multi_color("K1") && CrealityPrint::model_supports_multi_color("F022"), "upstream Creality models retain their capabilities");
    auto batch = CrealityPrint::start_print_commands(true, "/mnt/UDISK/printer_data/gcodes/test.gcode", {{"colorMatch_0", "T1A\tPLA\t#FF0000\t1\t0"}, {"colorMatch_1", "T1B\tPLA\t#FFFFFF\t1\t1"}, {"enableSelfTest", "1"}});
    require(batch.size() == 2, "CFS mapping and start are sent as an ordered two-command batch");
    require(json::parse(batch[0])["params"]["colorMatch"]["list"].size() == 2, "both explicit CFS mappings survive");
    require(json::parse(batch[1])["params"]["multiColorPrint"]["enableSelfTest"] == 1, "CFS start metadata");
    batch = CrealityPrint::start_print_commands(true, "/mnt/UDISK/printer_data/gcodes/test.gcode", {{"colorMatch_0", "T1A\tPLA\t#FFFFFF\t0\t0"}});
    require(batch.size() == 1 && json::parse(batch[0])["params"].contains("opGcodeFile"), "external spool uses the plain start command");
    batch = CrealityPrint::start_print_commands(true, "/mnt/UDISK/printer_data/gcodes/test.gcode", {});
    require(batch.size() == 1 && json::parse(batch[0])["params"].contains("opGcodeFile"), "beta plain-spool fallback is retained when no CFS mapping exists");
    bool mixed_rejected = false;
    try { CrealityPrint::start_print_commands(true, "test.gcode", {{"colorMatch_0", "T1A\tPLA\t#FFFFFF\t0\t0"}, {"colorMatch_1", "T1B\tPLA\t#FF0000\t1\t0"}}); }
    catch (const std::invalid_argument&) { mixed_rejected = true; }
    require(mixed_rejected, "an external spool cannot be mixed with CFS mappings");
    CrealityPrint host;
    host.response = R"({"boxsInfo":{"materialBoxs":[
      {"id":"1","type":0,"state":1,"humidity":"32","temp":28,"materials":[
        {"id":0,"state":1,"type":"PLA","color":"#0FF0000","name":"Red PLA","vendor":"Custom","rfid":"123","minTemp":"190","maxTemp":230,"pressure":"0.04"},
        {"id":1,"state":0,"type":"PLA"},
        {"id":2,"state":1,"type":""},
        {"id":3,"state":1,"type":"PETG","color":"#123456"}]},
      {"id":2,"type":0,"state":0,"materials":[{"id":0,"state":1,"type":"ABS"}]},
      {"id":3,"type":0,"state":1,"materials":[{"id":1,"state":1,"type":"ABS"}]},
      {"id":0,"type":1,"materials":[]}
    ]}})";
    auto slots = host.query_cfs_slots(false);
    require(slots.size() == 3, "only loaded slots from connected boxes are synced");
    require(slots[0].tool_id == "T1A" && slots[1].tool_id == "T1D" && slots[2].tool_id == "T3B", "physical slot identity survives empty slots and multiple boxes");
    require(slots[0].color == "#FF0000" && slots[1].color == "#123456", "Creality color normalization");
    require(slots[0].vendor == "Custom" && slots[0].rfid == "123" && slots[0].min_temp == 190 && slots[0].max_temp == 230, "slot material metadata");
    require(slots[0].pressure == 0.04 && slots[0].humidity == 32, "string and numeric protocol values");
    slots = host.query_cfs_slots(true);
    require(slots.size() == 4 && slots.back().external && !slots.back().loaded && slots.back().tool_id == "Ext", "empty external spool is available but not synced as loaded CFS");
    host.response = "bad json";
    require(host.query_cfs_slots(true).empty(), "malformed response fails without phantom slots");
    host.response = R"({"boxsInfo":{"materialBoxs":null}})";
    require(host.query_cfs_slots(true).empty(), "missing material boxes fail closed");
    host.response = R"({"boxsInfo":{"materialBoxs":[{"id":0,"type":1,"materials":[{"id":0,"type":"PLA","color":"#0ABCDEF"}]}]}})";
    slots = host.query_cfs_slots(true);
    require(slots.size() == 1 && slots[0].external && slots[0].loaded && slots[0].color == "#ABCDEF", "loaded external spool retains material");

    FixtureConfig original;
    original.colors.values = {"#FF0000", "#FFFFFF"};
    original.floats["flush_volumes_matrix"].values = {0,100,200,0};
    original.floats["flush_multiplier_fast"].values = {1.5};
    for (double multiplier : {0.0, 0.35, 0.7, 1.0, 1.3, 2.0}) {
        original.floats["flush_multiplier"].values = {multiplier};
        for (bool hi_without_host : {false, true}) {
            auto exported = export_matrix(original, PrimeVolumeMode::pvmDefault, !hi_without_host, hi_without_host);
            require(exported.floats.at("flush_multiplier").values == std::vector<double>{1.0}, "G-code multiplier must be 1, including values below 1 and offline Hi exports");
            require(exported.floats.at("flush_volumes_matrix").values == std::vector<double>({0, std::round(100*multiplier), std::round(200*multiplier), 0}), "selected multiplier is baked into volumes exactly once");
        }
        auto fast = export_matrix(original, PrimeVolumeMode::pvmFast, true, true);
        require(fast.floats.at("flush_volumes_matrix").values == std::vector<double>({0,150,300,0}), "Default multiplier never compounds with Fast purge");
        require(fast.floats.at("flush_multiplier").values == std::vector<double>{1.0}, "Fast export still emits multiplier 1");
        require(original.floats.at("flush_volumes_matrix").values == std::vector<double>({0,100,200,0}), "export cannot rewrite the project's base matrix");
    }
    auto other = export_matrix(original, PrimeVolumeMode::pvmDefault, false, false);
    require(other.floats.at("flush_multiplier").values == std::vector<double>{2.0}, "unrelated printers retain upstream multiplier metadata");
    auto single = original;
    single.colors.values = {"#FFFFFF"};
    single.floats["flush_volumes_matrix"].values = {};
    single.floats["flush_multiplier"].values = {0.7};
    single = export_matrix(single, PrimeVolumeMode::pvmDefault, true, true);
    require(single.floats.at("flush_multiplier").values == std::vector<double>{1.0}, "single-color exports also always say multiplier 1");
    original.floats["flush_volumes_matrix"].values = {0,100,200};
    bool rejected = false;
    try { export_matrix(original, PrimeVolumeMode::pvmDefault, true, true); }
    catch (const Slic3r::SlicingError&) { rejected = true; }
    require(rejected, "invalid matrix dimensions are rejected");
    std::cout << "PASS: CFS slot parsing and baked flush export (including multipliers below 1).\n";
}
'''
args.build.mkdir(parents=True, exist_ok=True)
unit = args.build/'ported_sections.cpp'
unit.write_text(prefix+host+helpers+models+query+commands+config+tests, encoding='utf-8')
exe = args.build/'ported_sections.exe'
subprocess.run([args.compiler, '-std=c++17', '-O1', '-I'+str(source/'deps_src'), str(unit), '-o', str(exe)], check=True)
subprocess.run([str(exe.resolve())], check=True)
