from datetime import datetime
from pathlib import Path
import hashlib
import json

class MetadataConverter():
    cache_path = Path(Path(__file__).parent.parent, "cache.json")

    def generate_new_metadata(self, generation_params: dict, subfolders: object, settings: object) -> tuple[bool, str, str]:
        exclude_list = {"prompt", "negativeprompt", "cfgscale", "steps", "sampler", "scheduler", "seed", "width", "height", "model", "loras"}

        prompt = generation_params["prompt"]
        negative_prompt = generation_params.get("negativeprompt", "")
        cfg = generation_params["cfgscale"]
        steps = generation_params["steps"]
        sampler = generation_params["sampler"] if generation_params.get("sampler") else "euler"
        scheduler = generation_params["scheduler"] if generation_params.get("scheduler") else "normal"
        seed = generation_params["seed"]
        size = f'{generation_params["width"]}x{generation_params["height"]}'
        model = generation_params["model"]

        errors = ""

        model_hash, model_errors = self.model_hash_generation(model, subfolders, settings)
        lora_hashes, lora_errors = self.lora_hash_generation(generation_params.get("loras", ""), subfolders, settings)

        if model_errors:
            errors += model_errors
        if lora_errors:
            if errors:
                errors += f"\n{lora_errors}"
            else:
                errors += lora_errors

        if errors:
          return False, "", errors
        
        new_metadata_string = f"{prompt}\nNegative prompt: {negative_prompt}\nSteps: {steps}, Sampler: {sampler}, Schedule type: {scheduler}, CFG scale: {cfg}, Seed: {seed}, Size: {size}, Model hash: {model_hash}, Model: {model},{lora_hashes}"

        for key, value in generation_params.items():
            if (key not in exclude_list):
                new_metadata_string += f" {key}: {value},"

        return True, new_metadata_string, ""

    def model_hash_generation(self, model: str, subfolders: object, settings: object) -> tuple[str, str]:
        sd_dir = Path(subfolders["Stable-Diffusion"])
        unet_dir = Path(subfolders["Unet"])

        if not sd_dir.exists():
            return "", "Couldn't find the Stable-Diffusion directory"
        if not unet_dir.exists():
            return "", "Couldn't find the unet directory"
        
        model_name = model.split("/")[-1]

        matching_sd_checkpoint = [file for ext in (f"{model_name}.safetensors", f"{model_name}.ckpt", f"{model_name}.sft") for file in sd_dir.rglob(ext)]
        matching_unet_checkpoint = [file for ext in (f"{model_name}.safetensors", f"{model_name}.ckpt", f"{model_name}.sft") for file in unet_dir.rglob(ext)]

        if len(matching_sd_checkpoint) == 0 and len(matching_unet_checkpoint) == 0:
            return "", "No Checkpoint found in Stable-Diffusion and unet folders"
        
        if len(matching_sd_checkpoint) > 0:
            return self.generate_hash(matching_sd_checkpoint[0], settings), ""
        else:
            return self.generate_hash(matching_unet_checkpoint[0], settings), ""

    def lora_hash_generation(self, loras: str, subfolders: object, settings: object) -> tuple[str, str]:
        if loras == "":
            return "", ""
        
        lora_list = loras.split(",")
        
        lora_dir = Path(subfolders["Lora"])

        if not lora_dir.exists():
            return "", "LoRAs were detected in the metadata but didn't find the LoRA folder."
        
        lora_hashes = ' Lora hashesh: "'

        for index, lora in enumerate(lora_list):
            lora_name = lora.split("/")[-1]
            matching_loras = [file for ext in (f"{lora_name}.safetensors", f"{lora_name}.ckpt") for file in lora_dir.rglob(ext)]

            if len(matching_loras) > 0:
                if index == len(lora_list) - 1:
                    lora_hashes += f'{matching_loras[0].stem}: {self.generate_hash(matching_loras[0], settings)}",'
                else:
                    lora_hashes += f"{matching_loras[0].stem}: {self.generate_hash(matching_loras[0], settings)}, "
            else:
                return "", "No LoRA in the specified LoraFolder path matched the metadata, please check again"
            
        return lora_hashes, ""

    def generate_hash(self, file_path: str, settings: object) -> str:
        filename = Path(file_path)

        if settings["cache"] and not self.cache_path.exists():
            self.generate_cache_file()

        if settings["cache"]:
            try:
                with open(self.cache_path, "r", encoding="utf-8") as cache_file:
                    cache_obj = json.load(cache_file)

                if cache_obj.get(filename.stem):
                    return cache_obj.get(filename.stem)
            except json.JSONDecodeError:
                self.cache_path.rename(Path(Path(__file__).parent.parent, f"cache_old_{int(datetime.now().timestamp() * 1000)}.json"))
                self.generate_cache_file()
                cache_obj = {}

        hash_sha256 = hashlib.sha256()
        blk_size = 1024 * 1024

        with open(filename, "rb") as file:
            for chunk in iter(lambda: file.read(blk_size), b""):
                hash_sha256.update(chunk)

        digested_hash = hash_sha256.hexdigest()[:10]

        if settings["cache"]:
            cache_obj[filename.stem] = digested_hash
            with open(self.cache_path, "w", encoding="utf-8") as cache_file:
                json.dump(cache_obj, cache_file, indent=2)

        print(f"Calculated hash for {filename.stem}: {digested_hash}")

        return digested_hash

    def generate_cache_file(self) -> None:
        with open(self.cache_path, "w", encoding="utf-8") as cache_file:
            json.dump({}, cache_file, indent=2)