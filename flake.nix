{
  description = "FableGUITemplate";
  nixConfig.bash-prompt = "\[nix-develop\]$ ";
  inputs.nixpkgs.url = "github:nixos/nixpkgs";
  inputs.flake-utils.url = "github:numtide/flake-utils";
  inputs.flake-compat = {
    url = github:edolstra/flake-compat;
    flake = false;
  };

  outputs = { self, nixpkgs, flake-utils, flake-compat }:
    flake-utils.lib.eachSystem [ "x86_64-linux" ] (system:
      let
        pkgs = import nixpkgs { 
          inherit system;
        };

        futTarget = "app.fut";
        name = "FableGUITemplate";
        version = let _ver = builtins.getEnv "GITVERSION_NUGETVERSIONV2"; in if _ver == "" then "0.0.0" else "${_ver}.${builtins.getEnv "GITVERSION_COMMITSSINCEVERSIONSOURCE"}";
        configArg = "";
        lockFile = ./fsharp/packages.lock.json;
        nugetSha256 = "sha256-ydTQ4VKJ7fqoRTNISl71PxG56syWZQFsnCTcacAdrA4=";
        project = "";
        FSharpOut = "App.fs.js";
        src = ./.;


        sdk = pkgs.dotnet-sdk;
        nodejs = pkgs.nodejs-17_x;
        fable-repo = pkgs.stdenv.mkDerivation {
          dontFetch = true;
          dontStrip = true;
          dontConfigure = true;
          dontPatch = true;
          dontInstall = true;
          dontBuild = true;
          pname = "fable-repo";
          version = "3.7.3";
          src = pkgs.fetchurl {
            sha256 = "sha256-2EW1SxiFflmIsXLdsQTWdj9dwSCV49RWcDtavff7g7g=";
            url = "https://www.nuget.org/api/v2/package/Fable/3.7.4";
          };
          unpackPhase = ''
            runHook preUnpack

            ${pkgs.unzip}/bin/unzip $src -d $out

            runHook postUnpack
          '';
        };


        fable = pkgs.writeShellApplication {
          name = "fable";
          text = ''
            #!${pkgs.bashInteractive}/bin/bash
            ${sdk}/bin/dotnet ${fable-repo}/tools/net5.0/any/fable.dll "$@"
          '';
        };

        nugetPackages-unpatched = pkgs.stdenv.mkDerivation {
          name = "${name}-${builtins.hashFile "sha1" lockFile}-${builtins.hashString "sha1" configArg}-nuget-pkgs-unpatched";

          outputHashAlgo = "sha256";
          outputHash = nugetSha256;
          outputHashMode = "recursive";

          nativeBuildInputs = [
              sdk
              pkgs.cacert
          ];

          dontFetch = true;
          dontUnpack = true;
          dontStrip = true;
          dontConfigure = true;
          dontPatch = true;
          dontBuild = true;
          DOTNET_CLI_TELEMETRY_OPTOUT=1;

          installPhase = ''
              set -e
              mkdir -p $out
              export HOME=$(mktemp -d)
              cp -R ${./fsharp} $HOME/tmp-sln
              chmod -R +rw $HOME/tmp-sln
              dotnet restore --locked-mode --use-lock-file${configArg} --lock-file-path "${lockFile}" --no-cache --nologo --packages $out $HOME/tmp-sln
            '';
          };

        depsWithRuntime = pkgs.symlinkJoin {
          name = "${name}-deps-with-runtime";
          paths = [ "${sdk}/shared" nugetPackages-unpatched ];
        };

        futlib = pkgs.stdenv.mkDerivation {
          name = "${name}-futlib";
          src = ./futhark;
          dontInstall = true;
          nativeBuildInputs = [ pkgs.haskellPackages.futhark pkgs.emscripten ];
          EMCFLAGS = "--memory-init-file 0 -flto -g0 -O3 -s ASSERTIONS=0 -s DYNAMIC_EXECUTION=0 -s WASM_ASYNC_COMPILATION=0 -s EVAL_CTORS=2 -s SINGLE_FILE=1 -s INVOKE_RUN=0 -s FULL_ES2=1 -s TEXTDECODER=2";
          buildPhase = ''
            mkdir -p $out
            futhark wasm --library ${futTarget} -o $out/${futTarget}
            sed -i 's/export {newFutharkContext/export {newFutharkContext, loadWASM/' $out/${futTarget}.mjs
          '';
        };

        fsharp = pkgs.stdenv.mkDerivation {
          name = "${name}-fsharp";
          src = ./fsharp;
          dontInstall = true;
          nativeBuildInputs = [ sdk ];
          DOTNET_CLI_TELEMETRY_OPTOUT=1;
          noAuditTmpdir = true;
          buildPhase = ''
              export HOME="$(mktemp -d)"
              mkdir -p $out
              dotnet restore --source ${depsWithRuntime} --nologo --locked-mode${configArg}  --use-lock-file --lock-file-path "${lockFile}" ${project}
              ${fable}/bin/fable -c Release --optimize --noRestore ${project}
              cp -r $PWD/fable_modules $out/fable_modules
              cp ${FSharpOut} $out/${FSharpOut}
          '';
        };

        package = pkgs.stdenv.mkDerivation {
          inherit name;
          inherit src;
          inherit version;
          dontStrip = true;
          dontPatch = true;
          dontInstall = true;
          distPhase = "true";
          nativeBuildInputs = [pkgs.esbuild];
          buildPhase = ''
              mkdir -p $out
              cp -r ${futlib}/. .
              cp -r ${fsharp}/. .
              esbuild --bundle --define:__dirname=\"/\" --define:import.meta.url=\"file:///App.fs.mjs\" --format=esm --outfile=$(basename ${FSharpOut} .js).mjs --platform=neutral --target=esnext --tree-shaking=true --external:path --external:fs --external:url --external:perf_hooks --external:os --external:readline --external:worker_threads $PWD/${FSharpOut}
              ${pkgs.nodePackages.terser}/bin/terser --comments all --module -c sequences=false,arguments,inline=false,reduce_vars=false,collapse_vars=false,keep_fargs=false,keep_infinity,module,passes=2,pure_getters=true,reduce_funcs=false,reduce_vars=false,toplevel,typeofs=false,unsafe,unsafe_arrows=true,ecma=2015,unsafe_math,unsafe_methods=true $(basename ${FSharpOut} .js).mjs | ${pkgs.nodePackages.js-beautify}/bin/js-beautify > $out/$(basename ${FSharpOut} .js).mjs
              sed -i '1s;^;#!${nodejs}/bin/node\n;' $out/$(basename ${FSharpOut} .js).mjs
              cat $out/$(basename ${FSharpOut} .js).mjs | ${pkgs.perl}/bin/perl -0777 -pe "s/export \{[^\}]+\};//igs" > $out/${FSharpOut}.global.js
              chmod +x $out/$(basename ${FSharpOut} .js).mjs
              chmod +x $out/${FSharpOut}.global.js
              cd $out
              tar -czf /tmp/${name}.tar.gz .
              cp /tmp/${name}.tar.gz $out
          '';
        };
      in rec {
          devShells.default = pkgs.mkShell {
            inherit name;
            doCheck = false;
            inherit version;
            DOTNET_CLI_TELEMETRY_OPTOUT=1;
            CLR_OPENSSL_VERSION_OVERRIDE=1.1;
            DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1;
            DOTNET_CLI_HOME = "/tmp/dotnet_cli";
            DOTNET_ROOT = "${sdk}";
            buildInputs = futlib.nativeBuildInputs ++ fsharp.nativeBuildInputs ++ [ nodejs ];
          };

          # Necessary for flake-compat
          devShell = devShells.default;

          checks.futlib = pkgs.stdenv.mkDerivation {
            src = ./futhark;
            nativeBuildInputs = futlib.nativeBuildInputs;
            name = "checks.futlib";
            doCheck = true;
            phases = [ "unpackPhase" "buildPhase" "checkPhase" ];
            buildPhase = ''
              mkdir -p $out
              touch $out/noop
            '';
            checkPhase = ''
              find . -name "*.actual" -type f -delete
              find . -name "*.expected" -type f -delete

              set +e
              futhark test --backend=c --compiled --no-terminal .
              if [ $? -ne 0 ]; then
                  echo "Checks failed"
                  echo "Expected: "
                  for i in *.expected ; do cat "$i" | futhark dataset --text; done
                  echo "Got: "
                  for i in *.actual ; do cat "$i" | futhark dataset --text; done
                  exit 1
              fi
            '';
          };
          packages.package = package;

          apps.package = {
            type = "app";
            program = "${package}/${builtins.replaceStrings [".js"] [".mjs"] FSharpOut}";
          };

          defaultApp = apps.package;

          packages.default = packages.package;
      }
    );
}