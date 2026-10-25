# enRAGE (NemesisMod.dll) - reconstructed source. `make help` lists the targets.
#
# Building needs only the .NET SDK and internet (NuGet + the pinned ScriptHookRDR2DotNet
# release). Paths for deploying and for the upstream targets come from config.mk
# (copy config.mk.example) or the command line, e.g. `make deploy GAME_DIR=/path/to/rdr2`.
# Paths with spaces are fine: they are only ever used quoted inside recipes.

-include config.mk

GAME_DIR     ?=
DATA_DIR     ?=
UPSTREAM_DIR ?= upstream
UPSTREAM_DLL ?= $(UPSTREAM_DIR)/NemesisMod.dll
NUGET_DIR    ?= $(HOME)/.nuget/packages

# ScriptHookRDR2DotNet V2 2.2.0 - the API version enRAGE 0.9.5.3 was built against
API_URL := https://github.com/Halen84/ScriptHookRDR2DotNet-V2/releases/download/2.2.0/ScriptHookRDR2DotNet-V2.rar
API_SHA := bfb7607d94b2de7f8530289377f163caad81098afc1fb7ee86191810a6cc6bba

OUT      := bin/Release/net48
DIST     := dist/scripts
TOOL      = dotnet tools/$(1)/bin/Release/net10.0/$(1).dll
TOOLS    := AsmDiff Layout Splitter
TOOL_DLLS = $(foreach t,$(TOOLS),tools/$(t)/bin/Release/net10.0/$(t).dll)
DOTNET_Q := -nologo -v q -tl:off -clp:NoSummary

# $(call need,VAR,path suffix,what it is)
need = @[ -n "$($(1))" ] && [ -e "$($(1))$(2)" ] || { echo "$(1) is not set or has no $(2) ($(3)) - set it in config.mk (see config.mk.example)"; exit 1; }
game_closed = @if pgrep -f 'RDR2\.exe' > /dev/null && [ -z "$(FORCE)" ]; then \
	  echo "RDR2 is running - quit it first (or FORCE=1, then press Insert in game to reload scripts)"; exit 1; fi

.PHONY: help fetch-api build check deploy deploy-original logs diff-upstream verify decompile tools clean

help:
	@echo "build            compile; dist/scripts/ is what goes into <game>/scripts/"
	@echo "check            build + static-initializer order check (run before every deploy)"
	@echo "deploy           check + copy dist/scripts/* into GAME_DIR/scripts (refuses while RDR2 runs)"
	@echo "logs             follow the mod's grudges.log (DATA_DIR) and Script Hook .NET's log"
	@echo "fetch-api        download ScriptHookRDRNetAPI.dll 2.2.0 into lib/ (build does this itself)"
	@echo "--- need the official enRAGE NemesisMod.dll in UPSTREAM_DIR (default upstream/):"
	@echo "deploy-original  put the official DLL back into the game"
	@echo "diff-upstream    what your edits change vs the official DLL (calls, fields, strings per method)"
	@echo "verify           reconstruction gate: build == official DLL apart from tools/asmdiff-baseline.txt"
	@echo "decompile        regenerate the source from UPSTREAM_DLL into build/upstream-src"
	@echo "clean            remove build output"

lib/ScriptHookRDRNetAPI.dll:
	@mkdir -p lib/.download
	curl -fsSL -o lib/.download/api.rar "$(API_URL)"
	@cd lib && if command -v bsdtar >/dev/null; then bsdtar -xf .download/api.rar ScriptHookRDRNetAPI.dll ScriptHookRDRNetAPI.xml; \
	  elif command -v unrar >/dev/null; then unrar e -o+ -inul .download/api.rar ScriptHookRDRNetAPI.dll ScriptHookRDRNetAPI.xml; \
	  elif command -v 7z >/dev/null; then 7z e -y -bso0 .download/api.rar ScriptHookRDRNetAPI.dll ScriptHookRDRNetAPI.xml; \
	  else echo "need bsdtar, unrar or 7z to unpack the .rar - or copy ScriptHookRDRNetAPI.dll from your game folder into lib/"; exit 1; fi
	@rm -rf lib/.download
	@echo "$(API_SHA)  lib/ScriptHookRDRNetAPI.dll" | (sha256sum -c --quiet 2>/dev/null || shasum -a 256 -c --quiet) \
	  || { echo "ScriptHookRDRNetAPI.dll checksum mismatch"; rm -f lib/ScriptHookRDRNetAPI.dll; exit 1; }

fetch-api: lib/ScriptHookRDRNetAPI.dll

build: lib/ScriptHookRDRNetAPI.dll
	dotnet build NemesisMod.csproj -c Release $(DOTNET_Q)
	@ls $(DIST)

tools: $(TOOL_DLLS)

define tool_rule
tools/$(1)/bin/Release/net10.0/$(1).dll: tools/$(1)/Program.cs tools/$(1)/$(1).csproj
	dotnet build tools/$(1) -c Release $(DOTNET_Q)
endef
$(foreach t,$(TOOLS),$(eval $(call tool_rule,$(t))))

# Partial-class field initializers run in file order: a static field that reads
# another during static init must come after it, in the same file.
check: build tools
	@$(call TOOL,Layout) $(OUT)/NemesisMod.dll NemesisSystem.NemesisMod --cctor-deps > build-cctor-deps.tsv
	@if grep -q 'read before assigned' build-cctor-deps.tsv; then \
	  echo "static init reads a field before it is set (move it into the same file, after its source):"; \
	  grep 'read before assigned' build-cctor-deps.tsv; rm build-cctor-deps.tsv; exit 1; fi
	@rm build-cctor-deps.tsv; echo "check: static init order OK"

deploy: check
	$(call need,GAME_DIR,/RDR2.exe,the game folder)
	$(game_closed)
	cp $(DIST)/* "$(GAME_DIR)/scripts/"
	@echo "deployed NemesisMod.dll $$(sha256sum $(DIST)/NemesisMod.dll | cut -c1-12) to $(GAME_DIR)/scripts"

deploy-original:
	$(call need,GAME_DIR,/RDR2.exe,the game folder)
	$(call need,UPSTREAM_DLL,,the official NemesisMod.dll)
	$(game_closed)
	cp "$(UPSTREAM_DLL)" "$(GAME_DIR)/scripts/NemesisMod.dll"
	rm -f "$(GAME_DIR)/scripts/NemesisMod.pdb"
	@echo "restored the official NemesisMod.dll"

logs:
	$(call need,DATA_DIR,/grudges.log,the enRAGE data folder)
	tail -n 40 -F "$(DATA_DIR)/grudges.log" "$(GAME_DIR)/ScriptHookRDRDotNet.log"

diff-upstream: build tools
	$(call need,UPSTREAM_DLL,,the official NemesisMod.dll)
	@$(call TOOL,AsmDiff) "$(UPSTREAM_DLL)" $(OUT)/NemesisMod.dll --by-method --verbose > build-asmdiff.txt || true
	@diff tools/asmdiff-baseline.txt build-asmdiff.txt && echo "no changes vs the official DLL beyond the known compiler differences" || true
	@rm -f build-asmdiff.txt

verify: build tools
	$(call need,UPSTREAM_DLL,,the official NemesisMod.dll)
	@$(call TOOL,AsmDiff) "$(UPSTREAM_DLL)" $(OUT)/NemesisMod.dll --by-method --verbose > build-asmdiff.txt || true
	@diff tools/asmdiff-baseline.txt build-asmdiff.txt > /dev/null && echo "verify: matches the official DLL (12 known compiler differences)" \
	  || { echo "verify: differs from the official DLL:"; diff tools/asmdiff-baseline.txt build-asmdiff.txt; rm -f build-asmdiff.txt; exit 1; }
	@rm -f build-asmdiff.txt

# Writes build/upstream-src/{src,Resources}; compare or copy over src/ yourself.
decompile: build tools
	$(call need,UPSTREAM_DLL,,the official NemesisMod.dll)
	tools/decompile.sh "$(UPSTREAM_DLL)" build/upstream-src lib \
	  "$(NUGET_DIR)/newtonsoft.json/13.0.3/lib/net45" \
	  "$(NUGET_DIR)/microsoft.netframework.referenceassemblies.net48/1.0.3/build/.NETFramework/v4.8"
	@echo "decompiled into build/upstream-src (diff -r build/upstream-src/src src)"

clean:
	rm -rf bin obj build dist tools/*/bin tools/*/obj
