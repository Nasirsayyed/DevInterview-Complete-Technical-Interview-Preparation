"""Company preparation packs.

All round descriptions and collections are *illustrative* preparation guides written by the DevInterview team, based on the
general, publicly described shape of interviews at large services and product companies. They are NOT claims that specific
questions were asked. Administrators can add Verified items with a cited source from the admin panel.
"""
from seedlib import write

SERVICES_ROUNDS = [
    ("Online assessment", "Aptitude (quantitative, logical reasoning, verbal) and basic programming questions in a timed online test."),
    ("Technical interview", "Programming fundamentals, OOP, DBMS/SQL, a short coding exercise and discussion of academic or work projects."),
    ("Managerial / HR interview", "Communication, teamwork, relocation and shift flexibility, career goals and situational questions."),
]
PRODUCT_ROUNDS = [
    ("Recruiter screen", "Background, motivation, role fit and logistics."),
    ("Coding rounds", "Data structures and algorithms problems solved live, with emphasis on clarifying, complexity analysis and clean code."),
    ("System design (experienced roles)", "Design a scalable service end to end and discuss trade-offs, bottlenecks and failure handling."),
    ("Behavioural interview", "Structured questions about past situations — prepare STAR stories on ownership, conflict, failure and impact."),
]

SERVICES_TOPICS = [("Programming fundamentals", "Loops, recursion, strings and arrays in your primary language.", "csharp"),
                   ("Object-oriented programming", "The four pillars, SOLID basics and simple class design.", "oop-design"),
                   ("SQL and DBMS", "Joins, keys, normalisation and simple queries.", "sql-server"),
                   ("Web basics", "HTTP, REST and the request lifecycle.", "web-apis")]
PRODUCT_TOPICS = [("Data structures & algorithms", "Arrays, hashing, trees, graphs and dynamic programming.", "dsa"),
                  ("System design", "Scalability, caching, partitioning and reliability.", "system-design"),
                  ("Language depth", "Concurrency, memory and the runtime of your main language.", "csharp"),
                  ("Behavioural", "Leadership, ownership and collaboration stories.", None)]

SERVICES_TECH = ["cs-001", "cs-006", "oo-001", "oo-002", "oo-005", "sq-001", "sq-012", "sq-020", "an-004", "ap-001", "js-002", "nw-001"]
SERVICES_CODING = ["ch-reverse-words", "ch-valid-palindrome", "ch-first-unique-char", "ch-climbing-stairs", "ch-sql-second-salary"]
PRODUCT_TECH = ["ds-004", "ds-006", "ds-012", "ds-014", "ds-016", "sd-003", "sd-006", "sd-009", "sd-012", "cs-017", "cs-020", "os-002"]
PRODUCT_CODING = ["ch-two-sum", "ch-number-of-islands", "ch-merge-intervals", "ch-lcs", "ch-sliding-window-max", "ch-shortest-path"]


def company(name, overview, industry, website, difficulty, kind, premium=True):
    services = kind == "services"
    return {
        "name": name, "overview": overview, "industry": industry, "website": website, "difficulty": difficulty, "premium": premium,
        "rounds": [{"name": n, "description": d} for n, d in (SERVICES_ROUNDS if services else PRODUCT_ROUNDS)],
        "topics": [{"name": n, "description": d, "tech": t} for n, d, t in (SERVICES_TOPICS if services else PRODUCT_TOPICS)],
        "collections": [
            {"title": f"{name}: technical fundamentals practice", "description": "Practice questions matching the topics this interview style emphasises.",
             "kind": "Technical", "questions": SERVICES_TECH if services else PRODUCT_TECH, "challenges": []},
            {"title": f"{name}: coding practice", "description": "Coding challenges at the difficulty this interview style typically targets.",
             "kind": "Coding", "questions": [], "challenges": SERVICES_CODING if services else PRODUCT_CODING},
            {"title": f"{name}: HR & behavioural preparation", "description": "Use the AI mock interview (HR type) with these themes: motivation, teamwork, conflict, learning from failure.",
             "kind": "Hr", "questions": [], "challenges": []},
        ],
    }


COMPANIES = [
    company("TCS", "Tata Consultancy Services is a large Indian multinational IT services and consulting company that hires many graduates each year.",
            "IT services", "https://www.tcs.com", "Beginner", "services", premium=False),
    company("Infosys", "Infosys is an Indian multinational IT services, consulting and outsourcing company with a large campus hiring programme.",
            "IT services", "https://www.infosys.com", "Beginner", "services", premium=False),
    company("Wipro", "Wipro is an Indian multinational technology services and consulting company.", "IT services", "https://www.wipro.com", "Beginner", "services"),
    company("Accenture", "Accenture is a global professional services company providing consulting, technology and operations services.",
            "Consulting", "https://www.accenture.com", "Intermediate", "services"),
    company("Cognizant", "Cognizant is a multinational IT services and consulting company.", "IT services", "https://www.cognizant.com", "Beginner", "services"),
    company("Capgemini", "Capgemini is a multinational consulting, technology services and digital transformation company headquartered in France.",
            "Consulting", "https://www.capgemini.com", "Intermediate", "services"),
    company("HCLTech", "HCLTech is an Indian multinational IT services and consulting company.", "IT services", "https://www.hcltech.com", "Beginner", "services"),
    company("Microsoft", "Microsoft is a multinational technology company known for Windows, Azure, Microsoft 365 and developer tools such as .NET and Visual Studio.",
            "Product / Cloud", "https://www.microsoft.com", "Advanced", "product"),
    company("Google", "Google, a subsidiary of Alphabet, builds search, advertising, Android, Google Cloud and many large-scale consumer products.",
            "Product / Cloud", "https://about.google", "Expert", "product"),
    company("Amazon", "Amazon is a multinational technology company spanning e-commerce, Amazon Web Services, devices and streaming.",
            "E-commerce / Cloud", "https://www.amazon.jobs", "Expert", "product"),
]

write("companies.json", COMPANIES)
