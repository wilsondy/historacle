package main

import (
	"bufio"
	"fmt"
	"log"
	"os"
	"regexp"
	"strconv"
	"strings"
)

func main() {
	file, err := os.Open(os.Args[1])
	if err != nil {
		log.Fatal(err)
	}
	defer file.Close()
	// Generation-27: Rendering Sequence-14
	sequenceBreak := regexp.MustCompile("Generation-([0-9]+): Rendering Sequence-([0-9]+)")
	// 2021-09-22 11:15:18.649: Sending: 'POST /api/v3/pet
	send := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Sending: '(POST|GET|PATCH|DELETE|PUT) ([^\s]+)`)
	//2021-09-22 11:15:18.738: Received: 'HTTP/1.1 500 Internal Server Error
	errorRegex := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received: 'HTTP/1.1 500 Internal Server Error`)
	//2021-09-23 11:35:27.255: Received: 'HTTP/1.1 400 Bad Request\r\
	badRequestRegex := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received: 'HTTP/1.1 400 Bad Request`)
	receivedRegex := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received`)
	replacer := regexp.MustCompile("/(fuzzstring|username.*)")
	scanner := bufio.NewScanner(file)
	SUT := "Server"
	CLIENT := "Client"
	// optionally, resize scanner's capacity for lines over 64K, see next example
	seqMap := make(map[string]int)
	currSeq := ""
	skip := false
	for scanner.Scan() {
		line := scanner.Text()

		if sequenceBreak.MatchString(line) {
			//fmt.Println(line)
			matches := sequenceBreak.FindAllStringSubmatch(line, -1)
			gen := matches[0][2]
			seq := matches[0][1]
			subSeq := seqMap[gen+seq]
			subSeq += 1
			currSeq = gen + "-" + seq + "-" + strconv.Itoa(subSeq)
			seqMap[gen+seq] = subSeq
			//skip = rand.Float64() > 0.1

			//fmt.Println(currSeq)
		}
		if skip {
			continue
		}
		if send.MatchString(line) {
			//fmt.Println(line)
			matches := send.FindStringSubmatch(line)
			//VERY HACKY - but we wash out unique ids - this is specific to the underlying dictionary used by Restler and is incredibly fragile
			matches[3] = replacer.ReplaceAllString(matches[3], "/userId")

			// fmt.Print("-")
			// fmt.Println()
			// fmt.Println(matches[2])
			// fmt.Println(matches[3])
			// fmt.Println("-")
			// date := matches[0][1]
			// method := matches[0][2]
			// url := matches[0][3]
			print(currSeq, CLIENT, SUT, matches[2]+matches[3])
		}
		if receivedRegex.MatchString(line) {
			fields := strings.Split(strings.ReplaceAll(line, "\\r\\n", "\n"), "\n")
			//fmt.Println(len(fields))
			body := fields[len(fields)-1]
			if errorRegex.MatchString(line) {
				//matches := errorRegex.FindStringSubmatch(line)
				print(currSeq, SUT, CLIENT, "ERROR"+body)
			} else if badRequestRegex.MatchString(line) {
				//matches := badRequestRegex.FindStringSubmatch(line)

				print(currSeq, SUT, CLIENT, "BAD_REQUEST"+body)
			} else {
				//	print(currSeq, SUT, CLIENT, "OK")

			}
		}

	}

	if err := scanner.Err(); err != nil {
		log.Fatal(err)
	}
}

func print(seq string, sender string, recv string, body string) {
	body = strings.ReplaceAll(body, " ", "")
	washErrorIds := regexp.MustCompile(`(\(ID:.*\))`)

	body = washErrorIds.ReplaceAllString(body, "(ERRID)")
	fmt.Println(seq + " " + sender + " " + recv + " " + body)
}
