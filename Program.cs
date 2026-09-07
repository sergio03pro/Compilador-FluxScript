using System;
using System.Collections.Generic;
using System.IO;

namespace FluxScriptCompiler
{
    // 1. Definición formal de Tokens basada en tu Tabla de Símbolos
    public enum TokenType
    {
        // Identificadores y Literales
        Identificador = 100,
        Entero = 101,
        Real = 102,
        Cadena = 103,

        // Operadores y Delimitadores
        Asignacion = 104,    // <-
        Suma = 105,          // +
        Resta = 106,         // -
        Multiplicacion = 107,// *
        Division = 108,      // /
        Menor = 109,         // <
        MenorIgual = 110,    // <=
        Mayor = 111,         // >
        MayorIgual = 112,    // >=
        Igualdad = 113,      // ==
        Diferente = 114,     // !=
        ParenIzq = 115,      // (
        ParenDer = 116,      // )
        DosPuntos = 117,     // :
        Coma = 118,          // ,
        SaltoLinea = 119,    // \n

        // Palabras Reservadas (200-209)
        Var = 200, Num = 201, Dec = 202, If = 203, Otherwise = 204,
        Loop = 205, Do = 206, End = 207, Ask = 208, Show = 209,

        // Errores Léxicos (500+)[cite: 3]
        ErrorSimbolo = 500,
        ErrorCadena = 501,
        ErrorRealMalFormado = 502,
        ErrorOperadorIncompleto = 503,

        EOF = 999
    }

    // 2. Estructura de la Tabla de Símbolos[cite: 1]
    public class Simbolo
    {
        public string Lexema { get; set; } = string.Empty;
        public TokenType Token { get; set; }
        public int Linea { get; set; }
        public string? TipoDato { get; set; } = null;
        public object? Valor { get; set; } = null;
    }

    // 3. Analizador Léxico (Scanner)
    public class Scanner
    {
        private readonly StringReader _reader;
        private int _lineaActual = 1;
        private char _caracterActual = '\0';

        public Dictionary<string, Simbolo> TablaSimbolos { get; private set; }

        // Matriz de Transiciones Completa: 25 Estados (0 al 24) x 20 Columnas
        // Valores >= 100: Aceptación directa consumiendo el carácter.
        // Valores <= -100: Aceptación con RETRACT (*) del carácter sobrante[cite: 1].
        // Valor -999: Retract especial para el fin de los comentarios.
        private readonly int[,] matrizTransiciones = new int[25, 20]
        {
            // Col:  L(0)  D(1)   .(2)   "(3)   <(4)   >(5)   =(6)   !(7)   -(8)   #(9)  \n(10) Esp(11) +(12)  *(13)  /(14)  ((15)  )(16)  :(17)  ,(18) Otro(19)
            /* 0  */ {   1,    2, -500,     5,     6,     7,     8,     9,    17,    10,    24,     0,    16,    18,    19,    20,    21,    22,    23, -500 },
            /* 1  */ {   1,    1, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100, -100 },
            /* 2  */ {-101,    2,    3, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101, -101 },
            /* 3  */ {-502,    4, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502, -502 },
            /* 4  */ {-102,    4, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102, -102 },
            /* 5  */ {   5,    5,    5,  103,    5,    5,    5,    5,    5,    5, -501,    5,    5,    5,    5,    5,    5,    5,    5,    5 },
            /* 6  */ {-109, -109, -109, -109, -109, -109,   12, -109,   11, -109, -109, -109, -109, -109, -109, -109, -109, -109, -109, -109 },
            /* 7  */ {-111, -111, -111, -111, -111, -111,   13, -111, -111, -111, -111, -111, -111, -111, -111, -111, -111, -111, -111, -111 },
            /* 8  */ {-500, -500, -500, -500, -500, -500,   14, -500, -500, -500, -500, -500, -500, -500, -500, -500, -500, -500, -500, -500 },
            /* 9  */ {-503, -503, -503, -503, -503, -503,   15, -503, -503, -503, -503, -503, -503, -503, -503, -503, -503, -503, -503, -503 },
            /* 10 */ {  10,   10,   10,   10,   10,   10,   10,   10,   10,   10, -999,   10,   10,   10,   10,   10,   10,   10,   10,   10 },
            
            // Filas 11 a 24: Estados terminales alcanzados. Obligan un Retract del siguiente carácter que se lea.
            /* 11 */ {-104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104, -104 },
            /* 12 */ {-110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110, -110 },
            /* 13 */ {-112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112, -112 },
            /* 14 */ {-113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113, -113 },
            /* 15 */ {-114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114, -114 },
            /* 16 */ {-105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105, -105 },
            /* 17 */ {-106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106, -106 },
            /* 18 */ {-107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107, -107 },
            /* 19 */ {-108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108, -108 },
            /* 20 */ {-115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115, -115 },
            /* 21 */ {-116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116, -116 },
            /* 22 */ {-117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117, -117 },
            /* 23 */ {-118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118, -118 },
            /* 24 */ {-119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119, -119 }
        };

        public Scanner(string codigoFuente)
        {
            _reader = new StringReader(codigoFuente);
            TablaSimbolos = new Dictionary<string, Simbolo>();
            InicializarPalabrasReservadas();
        }

        private void InicializarPalabrasReservadas()
        {
            RegistrarSimbolo("var", TokenType.Var);
            RegistrarSimbolo("num", TokenType.Num);
            RegistrarSimbolo("dec", TokenType.Dec);
            RegistrarSimbolo("if", TokenType.If);
            RegistrarSimbolo("otherwise", TokenType.Otherwise);
            RegistrarSimbolo("loop", TokenType.Loop);
            RegistrarSimbolo("do", TokenType.Do);
            RegistrarSimbolo("end", TokenType.End);
            RegistrarSimbolo("ask", TokenType.Ask);
            RegistrarSimbolo("show", TokenType.Show);
        }

        private void RegistrarSimbolo(string lexema, TokenType token)
        {
            if (!TablaSimbolos.ContainsKey(lexema))
            {
                TablaSimbolos.Add(lexema, new Simbolo { Lexema = lexema, Token = token, Linea = _lineaActual });
            }
        }

        private int ObtenerColumna(char c)
        {
            if (char.IsLetter(c)) return 0;
            if (char.IsDigit(c)) return 1;
            if (c == '.') return 2;
            if (c == '"') return 3;
            if (c == '<') return 4;
            if (c == '>') return 5;
            if (c == '=') return 6;
            if (c == '!') return 7;
            if (c == '-') return 8;
            if (c == '#') return 9;
            if (c == '\n') return 10;
            if (char.IsWhiteSpace(c)) return 11;
            if (c == '+') return 12;
            if (c == '*') return 13;
            if (c == '/') return 14;
            if (c == '(') return 15;
            if (c == ')') return 16;
            if (c == ':') return 17;
            if (c == ',') return 18;

            return 19; // Cualquier otro símbolo (OC)
        }

        private void LeerSiguiente()
        {
            int c = _reader.Read();
            _caracterActual = (c == -1) ? '\0' : (char)c;
        }

        public Simbolo GetToken()
        {
            int estadoActual = 0;
            string lexema = "";

            while (true)
            {
                if (_caracterActual == '\0')
                {
                    LeerSiguiente();
                    if (_caracterActual == '\0')
                    {
                        return new Simbolo { Lexema = "EOF", Token = TokenType.EOF, Linea = _lineaActual };
                    }
                }

                int columna = ObtenerColumna(_caracterActual);
                int transicion = matrizTransiciones[estadoActual, columna];

                // CASO 1: Retract especial de fin de comentario (-999)
                if (transicion == -999)
                {
                    estadoActual = 0;
                    lexema = "";
                    continue; 
                    // El salto de línea se conserva en _caracterActual para leerse en el siguiente ciclo.
                }

                // CASO 2: Terminal con RETRACT (Valores <= -100)
                if (transicion <= -100)
                {
                    TokenType tokenEncontrado = (TokenType)Math.Abs(transicion);
                    
                    // No consumimos _caracterActual (queda guardado para el próximo token)
                    // Si el token es identificador, verificamos si es palabra reservada O(1)[cite: 1]
                    if (tokenEncontrado == TokenType.Identificador)
                    {
                        if (TablaSimbolos.ContainsKey(lexema))
                        {
                            tokenEncontrado = TablaSimbolos[lexema].Token;
                        }
                        else
                        {
                            RegistrarSimbolo(lexema, tokenEncontrado);
                        }
                    }

                    // Avanzamos el contador si se acaba de emitir un Token de Salto de Línea
                    if (tokenEncontrado == TokenType.SaltoLinea)
                    {
                        int lineaEmitida = _lineaActual++;
                        return new Simbolo { Lexema = "\\n", Token = tokenEncontrado, Linea = lineaEmitida };
                    }

                    return new Simbolo { Lexema = lexema, Token = tokenEncontrado, Linea = _lineaActual };
                }

                // CASO 3: Terminal DIRECTO (Valores >= 100). Usado p. ej. al cerrar comillas.
                if (transicion >= 100)
                {
                    TokenType tokenEncontrado = (TokenType)transicion;
                    lexema += _caracterActual;
                    _caracterActual = '\0'; // Consumimos el carácter
                    return new Simbolo { Lexema = lexema, Token = tokenEncontrado, Linea = _lineaActual };
                }

                // CASO 4: Transición normal entre estados activos
                if (transicion == 0 && estadoActual == 0)
                {
                    // Estamos en el estado inicial y leímos un espacio en blanco -> se ignora
                    _caracterActual = '\0';
                    continue;
                }

                // Construimos el lexema y avanzamos de estado
                lexema += _caracterActual;
                estadoActual = transicion;
                _caracterActual = '\0'; // Consumimos para forzar la siguiente lectura
            }
        }
    }

    // 4. Módulo Principal de Prueba (Output de Consola)
    public class Program
    {
        public static void Main(string[] args)
        {
            // Código fuente en FluxScript (> 15 líneas) para validación final
            string codigoFuentePrueba = @"# Programa de prueba en FluxScript
var contador : num
var limite : num
var promedio : dec

contador <- 0
limite <- 15
promedio <- 0.0

loop contador < limite do
    contador <- contador + 1
    
    if contador == 10 do
        promedio <- 10.5
    otherwise
        promedio <- 1.0
    end
end

ask limite
show ""El proceso ha terminado""
";

            Scanner scanner = new Scanner(codigoFuentePrueba);
            Simbolo tokenActual = scanner.GetToken();

            Console.WriteLine("==================================================");
            Console.WriteLine("          CORRIENTE DE TOKENS (SCANNER)           ");
            Console.WriteLine("==================================================");

            while (tokenActual.Token != TokenType.EOF)
            {
                Console.WriteLine($"[Línea {tokenActual.Linea,2}] Lexema: {tokenActual.Lexema,-22} | Token ID: {(int)tokenActual.Token}");
                tokenActual = scanner.GetToken();
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine("          TABLA DE SÍMBOLOS GENERADA              ");
            Console.WriteLine("==================================================");

            foreach (var entrada in scanner.TablaSimbolos)
            {
                Console.WriteLine($"Lexema: {entrada.Value.Lexema,-15} | Token ID: {(int)entrada.Value.Token,3} | Tipo: {entrada.Value.TipoDato ?? "null",-5} | Valor: {entrada.Value.Valor ?? "null"}");
            }
        }
    }
}