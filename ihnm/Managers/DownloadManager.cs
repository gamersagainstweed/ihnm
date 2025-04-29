using SherpaOnnx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Compressors.BZip2;
using ihnm.Enums;
using System.Runtime.CompilerServices;
using AvaloniaEdit;
using static System.Net.WebRequestMethods;

namespace ihnm.Managers
{
    public static class DownloadManager
    {

        private static string sherpafolder = "sherpa/";

        public static List<sherpaTTSmodel> sherpaTTSmodels = new List<sherpaTTSmodel>()
        {
            new sherpaTTSmodel("vits-piper-en_US-glados",new EnumLanguage[] {EnumLanguage.English },78,1),
            new sherpaTTSmodel("kokoro-multi-lang-v1_0",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese,
            EnumLanguage.Spanish, EnumLanguage.French, EnumLanguage.Hindi, EnumLanguage.Italian, EnumLanguage.Japanese,
            EnumLanguage.Portuguese},382,53),
            new sherpaTTSmodel("kokoro-multi-lang-v1_1",new EnumLanguage[] {EnumLanguage.Chinese },406, 103),
            new sherpaTTSmodel("vits-piper-en_US-libritts-high", new EnumLanguage[] {EnumLanguage.English },147,904),
            new sherpaTTSmodel("vits-vctk", new EnumLanguage[] {EnumLanguage.English },188,109),
            //new sherpaTTSmodel("vits-cantonese-hf-xiaomaiiwn", new EnumLanguage[] {EnumLanguage.Cantonese },109,1),
            new sherpaTTSmodel("vits-piper-ar_JO-kareem-medium", new EnumLanguage[] {EnumLanguage.Arabic },77,1),
            new sherpaTTSmodel("vits-piper-ar_JO-kareem-low", new EnumLanguage[] {EnumLanguage.Arabic },77,1),
            new sherpaTTSmodel("vits-mimic3-af_ZA-google-nwu_low", new EnumLanguage[] {EnumLanguage.Afrikaans },90,1),
            new sherpaTTSmodel("vits-coqui-bn-custom_female", new EnumLanguage[] {EnumLanguage.Bengali },109,1),
            new sherpaTTSmodel("vits-mimic3-bn-multi_low", new EnumLanguage[] {EnumLanguage.Bengali },90,1),
            new sherpaTTSmodel("vits-coqui-bg-cv", new EnumLanguage[] {EnumLanguage.Bulgarian },68,1),
            new sherpaTTSmodel("vits-piper-ca_ES-upc_ona-medium", new EnumLanguage[] {EnumLanguage.Catalan },78,1),
            new sherpaTTSmodel("vits-piper-ca_ES-upc_pau-x_low", new EnumLanguage[] {EnumLanguage.Catalan },44,1),
            new sherpaTTSmodel("vits-piper-ca_ES-upc_ona-x_low", new EnumLanguage[] {EnumLanguage.Catalan },37,1),
            new sherpaTTSmodel("vits-coqui-hr-cv", new EnumLanguage[] {EnumLanguage.Croatian },68,1),
            new sherpaTTSmodel("vits-piper-cs_CZ-jirka-low", new EnumLanguage[] {EnumLanguage.Czech },78,1),
            new sherpaTTSmodel("vits-piper-cs_CZ-jirka-medium", new EnumLanguage[] {EnumLanguage.Czech },78,1),
            new sherpaTTSmodel("vits-coqui-cs-cv", new EnumLanguage[] {EnumLanguage.Czech },68,1),
            new sherpaTTSmodel("vits-coqui-da-cv", new EnumLanguage[] {EnumLanguage.Danish },68,1),
            new sherpaTTSmodel("vits-piper-da_DK-talesyntese-medium", new EnumLanguage[] {EnumLanguage.Danish },78,1),
            new sherpaTTSmodel("vits-coqui-nl-css10", new EnumLanguage[] {EnumLanguage.Dutch },68,1),
            new sherpaTTSmodel("vits-piper-nl_BE-nathalie-medium", new EnumLanguage[] {EnumLanguage.Dutch },78,1),
            new sherpaTTSmodel("vits-piper-nl_BE-nathalie-x_low", new EnumLanguage[] {EnumLanguage.Dutch },37,1),
            new sherpaTTSmodel("vits-piper-nl_BE-rdh-medium", new EnumLanguage[] {EnumLanguage.Dutch },78,1),
            new sherpaTTSmodel("vits-piper-nl_BE-rdh-x_low", new EnumLanguage[] {EnumLanguage.Dutch },37,1),
            new sherpaTTSmodel("vits-coqui-et-cv", new EnumLanguage[] {EnumLanguage.Estonian },68,1),
            new sherpaTTSmodel("vits-coqui-fi-css10", new EnumLanguage[] {EnumLanguage.Finnish },68,1),
            new sherpaTTSmodel("vits-piper-fi_FI-harri-low", new EnumLanguage[] {EnumLanguage.Finnish },84,1),
            new sherpaTTSmodel("vits-piper-fi_FI-harri-medium", new EnumLanguage[] {EnumLanguage.Finnish },78,1),
            new sherpaTTSmodel("vits-mimic3-fi_FI-harri-tapani-ylilammi_low", new EnumLanguage[] {EnumLanguage.Finnish },78,1),
            new sherpaTTSmodel("vits-coqui-fr-css10", new EnumLanguage[] {EnumLanguage.French },68,1),
            new sherpaTTSmodel("vits-piper-fr_FR-upmc-medium", new EnumLanguage[] {EnumLanguage.French },91,1),
            new sherpaTTSmodel("vits-piper-fr_FR-tom-medium", new EnumLanguage[] {EnumLanguage.French },78,1),
            new sherpaTTSmodel("vits-piper-fr_FR-siwis-low", new EnumLanguage[] {EnumLanguage.French },44,1),
            new sherpaTTSmodel("vits-piper-fr_FR-siwis-medium", new EnumLanguage[] {EnumLanguage.French },78,1),
            new sherpaTTSmodel("vits-piper-fr_FR-tjiho-model1", new EnumLanguage[] {EnumLanguage.French },78,1),
            new sherpaTTSmodel("vits-piper-fr_FR-tjiho-model2", new EnumLanguage[] {EnumLanguage.French },78,1),
            new sherpaTTSmodel("vits-piper-fr_FR-tjiho-model3", new EnumLanguage[] {EnumLanguage.French },78,1),
            new sherpaTTSmodel("vits-piper-ka_GE-natia-medium", new EnumLanguage[] {EnumLanguage.Georgian },78,1),
            new sherpaTTSmodel("vits-coqui-de-css10", new EnumLanguage[] {EnumLanguage.German},68,1),
            new sherpaTTSmodel("vits-piper-de_DE-thorsten_emotional-medium", new EnumLanguage[] {EnumLanguage.German},91,8),
            new sherpaTTSmodel("vits-piper-de_DE-eva_k-x_low", new EnumLanguage[] {EnumLanguage.German},37,1),
            new sherpaTTSmodel("vits-piper-de_DE-karlsson-low", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-kerstin-low", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-pavoque-low", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-ramona-low", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-thorsten-low", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-thorsten-medium", new EnumLanguage[] {EnumLanguage.German},78,1),
            new sherpaTTSmodel("vits-piper-de_DE-thorsten-high", new EnumLanguage[] {EnumLanguage.German},126,1),


            new sherpaTTSmodel("vits-piper-el_GR-rapunzelina-low", new EnumLanguage[] {EnumLanguage.Greek },78,1),

            new sherpaTTSmodel("vits-mimic3-gu_IN-cmu-indic_low", new EnumLanguage[] {EnumLanguage.Gujarati },90,1),
            new sherpaTTSmodel("vits-piper-hu_HU-anna-medium", new EnumLanguage[] {EnumLanguage.Hungarian },78,1),
            new sherpaTTSmodel("vits-piper-hu_HU-berta-medium", new EnumLanguage[] {EnumLanguage.Hungarian },78,1),
            new sherpaTTSmodel("vits-piper-hu_HU-imre-medium", new EnumLanguage[] {EnumLanguage.Hungarian },78,1),
            new sherpaTTSmodel("vits-mimic3-hu_HU-diana-majlinger_low", new EnumLanguage[] {EnumLanguage.Hungarian },78,1),


            new sherpaTTSmodel("vits-piper-is_IS-bui-medium", new EnumLanguage[] {EnumLanguage.Icelandic },91,1),
            new sherpaTTSmodel("vits-piper-is_IS-salka-medium", new EnumLanguage[] {EnumLanguage.Icelandic },91,1),
            new sherpaTTSmodel("vits-piper-is_IS-steinn-medium", new EnumLanguage[] {EnumLanguage.Icelandic },91,1),
            new sherpaTTSmodel("vits-piper-is_IS-ugla-medium", new EnumLanguage[] {EnumLanguage.Icelandic },91,1),

            new sherpaTTSmodel("vits-coqui-ga-cv", new EnumLanguage[] {EnumLanguage.Irish },68,1),

            new sherpaTTSmodel("vits-piper-it_IT-riccardo-x_low", new EnumLanguage[] {EnumLanguage.Italian},44,1),
            new sherpaTTSmodel("vits-piper-it_IT-paola-medium", new EnumLanguage[] {EnumLanguage.Italian },78,1),
            new sherpaTTSmodel("vits-piper-kk_KZ-iseke-x_low", new EnumLanguage[] {EnumLanguage.Kazakh },44,1),
            new sherpaTTSmodel("vits-piper-kk_KZ-issai-high", new EnumLanguage[] {EnumLanguage.Kazakh },140,1),
            new sherpaTTSmodel("vits-piper-kk_KZ-raya-x_low", new EnumLanguage[] {EnumLanguage.Kazakh },44,1),
            new sherpaTTSmodel("vits-mimic3-ko_KO-kss_low", new EnumLanguage[] {EnumLanguage.Korean },78,1),
            new sherpaTTSmodel("vits-piper-lv_LV-aivars-medium", new EnumLanguage[] {EnumLanguage.Latvian },78,1),
            new sherpaTTSmodel("vits-coqui-lv-cv", new EnumLanguage[] {EnumLanguage.Latvian },68,1),

            new sherpaTTSmodel("vits-coqui-lt-cv", new EnumLanguage[] {EnumLanguage.Lithuanian },68,1),
            new sherpaTTSmodel("vits-piper-lb_LU-marylux-medium", new EnumLanguage[] {EnumLanguage.Luxembourgish },78,1),
            new sherpaTTSmodel("vits-coqui-mt-cv", new EnumLanguage[] {EnumLanguage.Maltese },68,1),
            new sherpaTTSmodel("vits-piper-ne_NP-google-medium", new EnumLanguage[] {EnumLanguage.Nepali },91,1),
            new sherpaTTSmodel("vits-piper-ne_NP-google-x_low", new EnumLanguage[] {EnumLanguage.Nepali },44,1),
            new sherpaTTSmodel("vits-mimic3-ne_NP-ne-google_low", new EnumLanguage[] {EnumLanguage.Nepali },90,1),
            new sherpaTTSmodel("vits-piper-no_NO-talesyntese-medium", new EnumLanguage[] {EnumLanguage.Norwegian },78,1),
            new sherpaTTSmodel("vits-piper-fa_IR-amir-medium", new EnumLanguage[] {EnumLanguage.Persian },78,1),
            new sherpaTTSmodel("vits-piper-fa_IR-gyro-medium", new EnumLanguage[] {EnumLanguage.Persian },78,1),
            new sherpaTTSmodel("vits-mimic3-fa-haaniye_low", new EnumLanguage[] {EnumLanguage.Persian },78,1),
            new sherpaTTSmodel("vits-piper-fa_en-rezahedayatfar-ibrahimwalk-medium", new EnumLanguage[] {EnumLanguage.Persian },78,1),
            new sherpaTTSmodel("vits-coqui-pl-mai_female", new EnumLanguage[] {EnumLanguage.Polish },68,1),
            new sherpaTTSmodel("vits-piper-pl_PL-darkman-medium", new EnumLanguage[] {EnumLanguage.Polish },78,1),
            new sherpaTTSmodel("vits-piper-pl_PL-gosia-medium", new EnumLanguage[] {EnumLanguage.Polish },78,1),
            new sherpaTTSmodel("vits-piper-pl_PL-mc_speech-medium", new EnumLanguage[] {EnumLanguage.Polish },78,1),
            new sherpaTTSmodel("vits-mimic3-pl_PL-m-ailabs_low", new EnumLanguage[] {EnumLanguage.Polish },90,1),
            new sherpaTTSmodel("vits-coqui-pt-cv", new EnumLanguage[] {EnumLanguage.Portuguese },68,1),
            new sherpaTTSmodel("vits-piper-pt_BR-edresson-low", new EnumLanguage[] {EnumLanguage.Portuguese },78,1),
            new sherpaTTSmodel("vits-piper-pt_BR-faber-medium", new EnumLanguage[] {EnumLanguage.Portuguese },78,1),
            new sherpaTTSmodel("vits-piper-pt_PT-tugao-medium", new EnumLanguage[] {EnumLanguage.Portuguese },78,1),
            new sherpaTTSmodel("vits-coqui-ro-cv", new EnumLanguage[] {EnumLanguage.Romanian },68,1),
            new sherpaTTSmodel("vits-piper-ro_RO-mihai-medium", new EnumLanguage[] {EnumLanguage.Romanian },78,1),
            new sherpaTTSmodel("vits-piper-ru_RU-denis-medium", new EnumLanguage[] {EnumLanguage.Russian },78,1),
            new sherpaTTSmodel("vits-piper-ru_RU-dmitri-medium", new EnumLanguage[] {EnumLanguage.Russian },78,1),
            new sherpaTTSmodel("vits-piper-ru_RU-irina-medium", new EnumLanguage[] {EnumLanguage.Russian},78,1),
            new sherpaTTSmodel("vits-piper-ru_RU-ruslan-medium", new EnumLanguage[] {EnumLanguage.Russian},78,1),
            new sherpaTTSmodel("vits-piper-sr_RS-serbski_institut-medium", new EnumLanguage[] {EnumLanguage.Serbian },91,1),
            new sherpaTTSmodel("vits-coqui-sk-cv", new EnumLanguage[] {EnumLanguage.Slovak },68,1),
            new sherpaTTSmodel("vits-piper-sk_SK-lili-medium", new EnumLanguage[] {EnumLanguage.Slovak },78,1),
            new sherpaTTSmodel("vits-piper-sl_SI-artur-medium", new EnumLanguage[] {EnumLanguage.Slovenian },78,1),
            new sherpaTTSmodel("vits-coqui-sl-cv", new EnumLanguage[] {EnumLanguage.Slovenian },68,1),
            new sherpaTTSmodel("vits-piper-es-glados-medium", new EnumLanguage[] {EnumLanguage.Spanish },78,1),
            new sherpaTTSmodel("vits-piper-es_ES-carlfm-x_low", new EnumLanguage[] {EnumLanguage.Spanish },44,1),
            new sherpaTTSmodel("vits-piper-es_ES-davefx-medium", new EnumLanguage[] {EnumLanguage.Spanish },78,1),
            new sherpaTTSmodel("vits-piper-es_ES-sharvard-medium", new EnumLanguage[] {EnumLanguage.Spanish },91,1),
            new sherpaTTSmodel("vits-piper-es_MX-ald-medium", new EnumLanguage[] {EnumLanguage.Spanish},78,1),
            new sherpaTTSmodel("vits-piper-es_MX-claude-high", new EnumLanguage[] {EnumLanguage.Spanish },78,1),
            new sherpaTTSmodel("vits-mimic3-es_ES-m-ailabs_low", new EnumLanguage[] {EnumLanguage.Spanish },90,1),

            new sherpaTTSmodel("vits-piper-sw_CD-lanfrica-medium", new EnumLanguage[] {EnumLanguage.Swahili },78,1),
            new sherpaTTSmodel("vits-coqui-sv-cv", new EnumLanguage[] {EnumLanguage.Swedish },68,1),
            new sherpaTTSmodel("vits-piper-sv_SE-nst-medium", new EnumLanguage[] {EnumLanguage.Swedish },78,1),
            new sherpaTTSmodel("vits-mms-tha", new EnumLanguage[] {EnumLanguage.Thai },109,1),
            new sherpaTTSmodel("vits-mimic3-tn_ZA-google-nwu_low", new EnumLanguage[] {EnumLanguage.Tswana },91,1),
            new sherpaTTSmodel("vits-piper-tr_TR-dfki-medium", new EnumLanguage[] {EnumLanguage.Turkish },78,1),
            new sherpaTTSmodel("vits-piper-tr_TR-fahrettin-medium", new EnumLanguage[] {EnumLanguage.Turkish },78,1),
            new sherpaTTSmodel("vits-piper-tr_TR-fettah-medium", new EnumLanguage[] {EnumLanguage.Turkish },78,1),
            new sherpaTTSmodel("vits-piper-uk_UA-lada-x_low", new EnumLanguage[] {EnumLanguage.Ukrainian },37,1),
            new sherpaTTSmodel("vits-piper-vi_VN-25hours_single-low", new EnumLanguage[] {EnumLanguage.Vietnamese },78,1),
            new sherpaTTSmodel("vits-piper-vi_VN-vais1000-medium", new EnumLanguage[] {EnumLanguage.Vietnamese },78,1),
            new sherpaTTSmodel("vits-piper-vi_VN-vivos-x_low", new EnumLanguage[] {EnumLanguage.Vietnamese },78,1),
            new sherpaTTSmodel("vits-mimic3-vi_VN-vais1000_low", new EnumLanguage[] {EnumLanguage.Vietnamese },78,1),
            new sherpaTTSmodel("vits-piper-cy_GB-gwryw_gogleddol-medium", new EnumLanguage[] {EnumLanguage.Welsh },78,1),
            new sherpaTTSmodel("vits-mms-nan", new EnumLanguage[] {EnumLanguage.Minnan },109,1),
        };

        public static List<sherpaSTTmodel> sherpaSTTmodels = new List<sherpaSTTmodel>()
        {
            new sherpaSTTmodel("sherpa-onnx-zipformer-zh-en-2023-11-22",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese },324),
            new sherpaSTTmodel("sherpa-onnx-zipformer-gigaspeech-2023-12-12",new EnumLanguage[] {EnumLanguage.English},321),
            new sherpaSTTmodel("sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",new EnumLanguage[] {EnumLanguage.Japanese},740),
            new sherpaSTTmodel("sherpa-onnx-zipformer-korean-2024-06-24",new EnumLanguage[] {EnumLanguage.Korean},343),
            new sherpaSTTmodel("sherpa-onnx-zipformer-thai-2024-06-20",new EnumLanguage[] {EnumLanguage.Thai},724),
            new sherpaSTTmodel("sherpa-onnx-zipformer-cantonese-2024-03-13",new EnumLanguage[] {EnumLanguage.Cantonese},339),
            new sherpaSTTmodel("sherpa-onnx-zipformer-multi-zh-hans-2023-9-2",new EnumLanguage[] {EnumLanguage.Chinese},325),
            //new sherpaSTTmodel("icefall-asr-cv-corpus-13.0-2023-03-09-en-pruned-transducer-stateless7-2023-04-17",new EnumLanguage[] {EnumLanguage.English},415),
            //new sherpaSTTmodel("icefall-asr-zipformer-wenetspeech-20230615",new EnumLanguage[] {EnumLanguage.Chinese},382),
            new sherpaSTTmodel("sherpa-onnx-zipformer-ru-2024-09-18",new EnumLanguage[] {EnumLanguage.Russian},317),
            new sherpaSTTmodel("sherpa-onnx-small-zipformer-ru-2024-09-18",new EnumLanguage[] {EnumLanguage.Russian},115),
            new sherpaSTTmodel("sherpa-onnx-zipformer-large-en-2023-06-26",new EnumLanguage[] {EnumLanguage.English},714),
            new sherpaSTTmodel("sherpa-onnx-zipformer-small-en-2023-06-26",new EnumLanguage[] {EnumLanguage.English},117),
            new sherpaSTTmodel("sherpa-onnx-zipformer-en-2023-06-26",new EnumLanguage[] {EnumLanguage.English},318),
            //new sherpaSTTmodel("icefall-asr-multidataset-pruned_transducer_stateless7-2023-05-04",new EnumLanguage[] {EnumLanguage.English},413),
            new sherpaSTTmodel("sherpa-onnx-zipformer-en-2023-04-01",new EnumLanguage[] {EnumLanguage.English},521),
            new sherpaSTTmodel("sherpa-onnx-zipformer-en-2023-03-30",new EnumLanguage[] {EnumLanguage.English},521),
            new sherpaSTTmodel("sherpa-onnx-zipformer-vi-2025-04-20",new EnumLanguage[] {EnumLanguage.Vietnamese},0),
            

            new sherpaSTTmodel("sherpa-onnx-conformer-zh-stateless2-2023-05-23",new EnumLanguage[] {EnumLanguage.Chinese},474),
            new sherpaSTTmodel("sherpa-onnx-conformer-en-2023-03-18",new EnumLanguage[] {EnumLanguage.English},442),

            new sherpaSTTmodel("sherpa-onnx-paraformer-trilingual-zh-cantonese-en",new EnumLanguage[] {EnumLanguage.English,
                EnumLanguage.Chinese, EnumLanguage.Cantonese},1066),
            new sherpaSTTmodel("sherpa-onnx-paraformer-en-2024-03-09",new EnumLanguage[] {EnumLanguage.English},1037),
            new sherpaSTTmodel("sherpa-onnx-paraformer-zh-small-2024-03-09",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese},80),
            new sherpaSTTmodel("sherpa-onnx-paraformer-zh-2024-03-09",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese},1003),
            new sherpaSTTmodel("sherpa-onnx-paraformer-zh-2023-03-28",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese},1039),
            new sherpaSTTmodel("sherpa-onnx-paraformer-zh-2023-09-14",new EnumLanguage[] {EnumLanguage.English, EnumLanguage.Chinese},234),

            new sherpaSTTmodel("sherpa-onnx-nemo-ctc-en-citrinet-512",new EnumLanguage[] {EnumLanguage.English},180),
            new sherpaSTTmodel("sherpa-onnx-nemo-ctc-en-conformer-small",new EnumLanguage[] {EnumLanguage.English},127),
            new sherpaSTTmodel("sherpa-onnx-nemo-ctc-en-conformer-medium",new EnumLanguage[] {EnumLanguage.English},218),
            new sherpaSTTmodel("sherpa-onnx-nemo-ctc-en-conformer-large",new EnumLanguage[] {EnumLanguage.English},670),

            new sherpaSTTmodel("sherpa-onnx-whisper-tiny.en",new EnumLanguage[] {EnumLanguage.English},245),
            new sherpaSTTmodel("sherpa-onnx-whisper-base.en",new EnumLanguage[] {EnumLanguage.English},433),
            new sherpaSTTmodel("sherpa-onnx-whisper-small.en",new EnumLanguage[] {EnumLanguage.English},1283),
            new sherpaSTTmodel("sherpa-onnx-whisper-medium.en",new EnumLanguage[] {EnumLanguage.English},3820),
            new sherpaSTTmodel("sherpa-onnx-whisper-distil-small.en",new EnumLanguage[] {EnumLanguage.English},921),
            new sherpaSTTmodel("sherpa-onnx-whisper-tiny",new EnumLanguage[] {EnumLanguage.English},245),
            new sherpaSTTmodel("sherpa-onnx-whisper-base",new EnumLanguage[] {EnumLanguage.English},433),
            new sherpaSTTmodel("sherpa-onnx-whisper-small",new EnumLanguage[] {EnumLanguage.English},1283),
            new sherpaSTTmodel("sherpa-onnx-whisper-medium",new EnumLanguage[] {EnumLanguage.English},3820),
            new sherpaSTTmodel("sherpa-onnx-whisper-large-v3",new EnumLanguage[] {EnumLanguage.English},1695),
            new sherpaSTTmodel("sherpa-onnx-whisper-large-v2",new EnumLanguage[] {EnumLanguage.English},1695),
            new sherpaSTTmodel("sherpa-onnx-whisper-large-v1",new EnumLanguage[] {EnumLanguage.English},1695),

            new sherpaSTTmodel("sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17",new EnumLanguage[] {EnumLanguage.English,
                EnumLanguage.Chinese, EnumLanguage.Cantonese, EnumLanguage.Japanese, EnumLanguage.Korean
            },1124),

            new sherpaSTTmodel("sherpa-onnx-fire-red-asr-large-zh_en-2025-02-16",new EnumLanguage[] {EnumLanguage.English,
                EnumLanguage.Chinese
            },1661),

            new sherpaSTTmodel("sherpa-onnx-dolphin-base-ctc-multi-lang-2025-04-02",new EnumLanguage[] {EnumLanguage.English,
                EnumLanguage.Chinese, EnumLanguage.Cantonese, EnumLanguage.Japanese, EnumLanguage.Korean, EnumLanguage.Thai,
                EnumLanguage.Russian, EnumLanguage.Vietnamese, EnumLanguage.Arabic, EnumLanguage.Persian, EnumLanguage.Bengali,
                EnumLanguage.Gujarati, EnumLanguage.Kazakh, EnumLanguage.Nepali
            },303),

            new sherpaSTTmodel("sherpa-onnx-dolphin-small-ctc-multi-lang-2025-04-02",new EnumLanguage[] {EnumLanguage.English,
                EnumLanguage.Chinese, EnumLanguage.Cantonese, EnumLanguage.Japanese, EnumLanguage.Korean, EnumLanguage.Thai,
                EnumLanguage.Russian, EnumLanguage.Vietnamese, EnumLanguage.Arabic, EnumLanguage.Persian, EnumLanguage.Bengali,
                EnumLanguage.Gujarati, EnumLanguage.Kazakh, EnumLanguage.Nepali
            },784),




        };

        public static List<sherpaVADmodel> sherpaVADmodels = new List<sherpaVADmodel>()
        {
             new sherpaVADmodel("silero_vad",2),
             new sherpaVADmodel("silero_vad_v5",2),
             new sherpaVADmodel("silero_vad_v4",2),
        };

        public static bool isTTSmodel(string model)
        {
            foreach (sherpaTTSmodel mdl in sherpaTTSmodels)
            {
                if (mdl.id == model)
                    return true;
            }
            return false;
        }

        public static bool isSTTmodel(string model)
        {
            foreach (sherpaSTTmodel mdl in sherpaSTTmodels)
            {
                if (mdl.id == model)
                    return true;
            }
            return false;
        }

        public static bool isVADmodel(string model)
        {
            foreach (sherpaVADmodel mdl in sherpaVADmodels)
            {
                if (mdl.id == model)
                    return true;
            }
            return false;
        }

        public class sherpaTTSmodel
        {
            public string id;
            public EnumLanguage[] languages;
            public int filesize;
            public int speakers;

            public sherpaTTSmodel(string id, EnumLanguage[] languages, int filesize, int speakers)
            {
                this.id = id;
                this.languages = languages;
                this.filesize = filesize;
                this.speakers = speakers;
            }

        }

        public class sherpaSTTmodel
        {
            public string id;
            public EnumLanguage[] languages;
            public int filesize;

            public sherpaSTTmodel(string id, EnumLanguage[] languages, int filesize)
            {
                this.id = id;
                this.languages = languages;
                this.filesize = filesize;
            }

        }

        public class sherpaVADmodel
        {
            public string id;
            public int filesize;

            public sherpaVADmodel(string id, int filesize)
            {
                this.id = id;
                this.filesize = filesize;
            }

        }


        private static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });

        public static async Task<Stream> GetFileAsync(string requestUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage obj = await httpClient.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            obj.EnsureSuccessStatusCode();
            return await obj.Content.ReadAsStreamAsync(cancellationToken);
        }

        public static async Task DownloadTarBZ2(string dir, string requestUri)
        {

            string[] uriArray = requestUri.Split('/');

            string requestFilename = uriArray[^1];

            CallOutput($"Downloading file {requestFilename}");

            Console.WriteLine($"Downloading file {requestFilename}");

            Stream modelStream;

            modelStream = await GetFileAsync(requestUri);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fileWriter = System.IO.File.OpenWrite(dir+requestFilename);
            await modelStream.CopyToAsync(fileWriter);
            modelStream.Dispose();
            fileWriter.Close();
            CallOutput($"Unpacking {requestFilename}");


            using FileStream fs = new(dir + requestFilename,  FileMode.Open, FileAccess.Read);
            using BZip2Stream bz = new(fs, SharpCompress.Compressors.CompressionMode.Decompress,false);


            TarFile.ExtractToDirectory(bz, dir, true);

            fs.Close();

            System.IO.File.Delete(dir+requestFilename);

            CallOutput($"Unpacked {requestFilename}");

        }

        public static async Task DownloadONNX(string dir, string requestUri)
        {

            string[] uriArray = requestUri.Split('/');

            string requestFilename = uriArray[^1];

            CallOutput($"Downloading file {requestFilename}");

            Console.WriteLine($"Downloading file {requestFilename}");

            Stream modelStream;

            modelStream = await GetFileAsync(requestUri);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Directory.CreateDirectory(dir + Path.GetFileNameWithoutExtension(requestFilename));

            using var fileWriter = System.IO.File.OpenWrite(dir + Path.GetFileNameWithoutExtension(requestFilename) 
                + "/"+requestFilename);
            await modelStream.CopyToAsync(fileWriter);
            modelStream.Dispose();
            fileWriter.Close();
            CallOutput($"Downloaded {requestFilename}");

        }


        public static async void DownloadModels(List<string> models)
        {
            foreach (string model in models) {


                if (isSTTmodel(model))
                {
                    await DownloadTarBZ2(sherpafolder+"stt-models/",
                    "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/" + model + ".tar.bz2");
                }
                else if (isTTSmodel(model))
                {
                    await DownloadTarBZ2(sherpafolder+"tts-models/",
                    "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/" + model + ".tar.bz2");
                }
                else if (isVADmodel(model))
                {
                    await DownloadONNX(sherpafolder + "vad-models/",
                    "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/" + model + ".onnx");
                }

            }
            CallOutput("All selected models have been downloaded and unpacked");
            RaiseDownloadComplete();
        }

        public static async void DownloadSounds()
        {
            await DownloadTarBZ2("./",
        "https://archive.org/download/ihnmsounds.tar/sounds.tar.bz2");

        }

        public class OutputEventArgs : EventArgs
        {
            public string text { get; set; }
        }


        private static void CallOutput(string text)
        {
            OutputEventArgs e = new OutputEventArgs();
            e.text = text;
            Output?.Invoke(null, e);
        }

        public delegate void OutputEventHandler(object myObject, OutputEventArgs myArgs);

        public static event OutputEventHandler Output;


        private static void RaiseDownloadComplete()
        {
            DownloadComplete?.Invoke(null, null);
        }

        public static event EventHandler DownloadComplete;

    }
}
